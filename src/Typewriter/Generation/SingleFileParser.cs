using EnvDTE;
using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.Design;
using Typewriter.CodeModel;
using Typewriter.TemplateEditor.Lexing;
using Typewriter.VisualStudio;
using Type = System.Type;

namespace Typewriter.Generation
{
    public class SingleFileParser
    {
        public static string Parse(ProjectItem projectItem, File[] files, string template, List<Type> extensions, out bool success)
        {
            var instance = new SingleFileParser(extensions);
            var output = instance.ParseTemplate(projectItem, template, files);
            success = instance.hasError == false;

            return instance.matchFound ? output : null;
        }

        private readonly List<Type> extensions;
        private bool matchFound;
        private bool hasError;

        private SingleFileParser(List<Type> extensions)
        {
            this.extensions = extensions;
        }

        private string ParseTemplate(ProjectItem projectItem, string template, File[] files, object context = null)
        {
            if (string.IsNullOrEmpty(template)) return null;

            var output = string.Empty;
            var stream = new Stream(template);

            while (stream.Advance())
            {
                if (ParseDollar(projectItem, template, files, stream, context, ref output)) continue;
                output += stream.Current;
            }

            return output;
        }

        private bool ParseDollar(ProjectItem projectItem, string template, File[] files, Stream stream, object context, ref string output)
        {
            if (stream.Current == '$')
            {
                var identifier = stream.PeekWord(1);

                var blockStream  = 

                stream.Advance(identifier.Length);
                int advance = 0;
                int offset = stream.Position + 1;
                int index = 0;
                foreach (var f in files)
                {

                    // Create new stream for each file
                    var innerStream = new Stream(template);
                    innerStream.Advance(offset);
                   

                    string sourcePath = f.FullName;
                    context = f;

                    if (TryGetIdentifier(projectItem, sourcePath, identifier, context, out var value))
                    {
                         
                        if (value is IEnumerable<Item> collection)
                        {
                            var filter = ParseBlock(innerStream, '(', ')');
                            var block = ParseBlock(innerStream, '[', ']');
                            var separator = ParseBlock(innerStream, '[', ']');

                            if (filter == null && block == null && separator == null)
                            {
                                var stringValue = value.ToString();

                                if (stringValue != value.GetType().FullName)
                                    output += stringValue;
                                else
                                    output += "$" + identifier;
                            }
                            else
                            {
                                IEnumerable<Item> items = ApplyFilter(collection, filter, projectItem, identifier, sourcePath);
                                
                                output += string.Join(ParseTemplate(projectItem, sourcePath, separator, context), items.Select(item => ParseTemplate(projectItem, sourcePath, block, item)));
                                // In this case we mus check if  we get items of the next element too
                                for (int i= index+1; i<files.Length; i++)
                                {

                                    var next = files[i];

                                   
                                    if (next != null)
                                    {
                                        if (TryGetIdentifier(projectItem, next.FullName, identifier, next, out var vnext))
                                        {
                                            if (vnext is IEnumerable<Item> colnext)
                                            {
                                                

                                                colnext = ApplyFilter(colnext, filter, projectItem, identifier, next.FullName);

                                                if (colnext.Any())
                                                {
                                                    

                                                    output += ParseTemplate(projectItem, sourcePath, separator, context);

                                                    break;
                                                }
                                            }
                                        }

                                    }
                                }
                            }
                        }
                        else if (value is bool)
                        {
                            var trueBlock = ParseBlock(innerStream, '[', ']');
                            var falseBlock = ParseBlock(innerStream, '[', ']');

                            output += ParseTemplate(projectItem, sourcePath, (bool)value ? trueBlock : falseBlock, context);
                        }
                        else
                        {
                            var block = ParseBlock(innerStream, '[', ']');
                            if (value != null)
                            {
                                if (block != null)
                                {
                                    output += ParseTemplate(projectItem, sourcePath, block, value);
                                }
                                else
                                {
                                    output += value.ToString();
                                }
                            }
                        } 
                    }

                    advance = innerStream.Position+1;
                    index++;
                }

                bool canAdvance = stream.Advance(advance - offset);

                return true;
            }

            return false;
        }

        private IEnumerable<Item> ApplyFilter(IEnumerable<Item> collection, string filter, ProjectItem projectItem, string identifier, string sourcePath)
        {
            IEnumerable<Item> items;
            if (filter != null && filter.StartsWith("$", StringComparison.OrdinalIgnoreCase))
            {
                var predicate = filter.Remove(0, 1);
                if (extensions != null)
                {
                    // Lambda filters are always defined in the first extension type
                    var c = extensions.FirstOrDefault()?.GetMethod(predicate);
                    if (c != null)
                    {
                        try
                        {
                            items = collection.Where(x => (bool)c.Invoke(null, new object[] { x })).ToList();
                            matchFound = matchFound || items.Any();
                        }
                        catch (Exception e)
                        {
                            items = Array.Empty<Item>();
                            hasError = true;

                            var message = $"Error rendering template. Cannot apply filter to identifier '{identifier}'.";
                            LogException(e, message, projectItem, sourcePath);
                        }
                    }
                    else
                    {
                        items = Array.Empty<Item>();
                    }
                }
                else
                {
                    items = Array.Empty<Item>();
                }
            }
            else
            {
                items = ItemFilter.Apply(collection, filter, ref matchFound);
            }


            return items;
        }

        private static string ParseBlock(Stream stream, char open, char close, bool onlyPeek = false)
        {
            if (stream.Peek() == open)
            {
                var block = stream.PeekBlock(2, open, close);

                if (!onlyPeek)
                {
                    stream.Advance(block.Length);
                    stream.Advance(stream.Peek(2) == close ? 2 : 1);
                }

                return block;
            }

            return null;
        }

        private bool TryGetIdentifier(ProjectItem projectItem, string sourcePath, string identifier, object context, out object value)
        {
            value = null;

            if (identifier == null) return false;

            var type = context.GetType();

            try
            {
                var property = type.GetProperty(identifier);
                if (property != null)
                {
                    value = property.GetValue(context);
                    return true;
                }

                var extension = extensions.Select(e => e.GetMethod(identifier, new[] { type })).FirstOrDefault(m => m != null);
                if (extension != null)
                {
                    value = extension.Invoke(null, new[] { context });
                    return true;
                }
            }
            catch (Exception e)
            {
                hasError = true;

                var message = $"Error rendering template. Cannot get identifier '{identifier}'.";
                LogException(e, message, projectItem, sourcePath);
            }

            return false;
        }

        private void LogException(Exception exception, string message, ProjectItem projectItem, string sourcePath)
        {
            // skip the target invokation exception, get the real exception instead.
            if (exception is TargetInvocationException && exception.InnerException != null)
            {
                exception = exception.InnerException;
            }

            var studioMessage = $"{message} Error: {exception.Message}. Source path: {sourcePath}. See Typewriter output for more detail.";
            var logMessage = $"{message} Source path: {sourcePath}{Environment.NewLine}{exception}";

            Log.Error(logMessage);
            ErrorList.AddError(projectItem, studioMessage);
            ErrorList.Show();
        }


        private string ParseTemplate(ProjectItem projectItem, string sourcePath, string template, object context)
        {
            if (string.IsNullOrEmpty(template)) return null;

            var output = string.Empty;
            var stream = new Stream(template);

            while (stream.Advance())
            {
                if (ParseDollar(projectItem, sourcePath, stream, context, ref output)) continue;
                output += stream.Current;
            }

            return output;
        }

        private bool ParseDollar(ProjectItem projectItem, string sourcePath, Stream stream, object context, ref string output)
        {
            if (stream.Current == '$')
            {
                var identifier = stream.PeekWord(1);

                if (TryGetIdentifier(projectItem, sourcePath, identifier, context, out var value))
                {
                    stream.Advance(identifier.Length);

                    if (value is IEnumerable<Item> collection)
                    {
                        var filter = ParseBlock(stream, '(', ')');
                        var block = ParseBlock(stream, '[', ']');
                        var separator = ParseBlock(stream, '[', ']');

                        if (filter == null && block == null && separator == null)
                        {
                            var stringValue = value.ToString();

                            if (stringValue != value.GetType().FullName)
                                output += stringValue;
                            else
                                output += "$" + identifier;
                        }
                        else
                        {
                            IEnumerable<Item> items;
                            if (filter != null && filter.StartsWith("$", StringComparison.OrdinalIgnoreCase))
                            {
                                var predicate = filter.Remove(0, 1);
                                if (extensions != null)
                                {
                                    // Lambda filters are always defined in the first extension type
                                    var c = extensions.FirstOrDefault()?.GetMethod(predicate);
                                    if (c != null)
                                    {
                                        try
                                        {
                                            items = collection.Where(x => (bool)c.Invoke(null, new object[] { x })).ToList();
                                            matchFound = matchFound || items.Any();
                                        }
                                        catch (Exception e)
                                        {
                                            items = Array.Empty<Item>();
                                            hasError = true;

                                            var message = $"Error rendering template. Cannot apply filter to identifier '{identifier}'.";
                                            LogException(e, message, projectItem, sourcePath);
                                        }
                                    }
                                    else
                                    {
                                        items = Array.Empty<Item>();
                                    }
                                }
                                else
                                {
                                    items = Array.Empty<Item>();
                                }
                            }
                            else
                            {
                                items = ItemFilter.Apply(collection, filter, ref matchFound);
                            }
                            output += string.Join(ParseTemplate(projectItem, sourcePath, separator, context), items.Select(item => ParseTemplate(projectItem, sourcePath, block, item)));
                        }
                    }
                    else if (value is bool)
                    {
                        var trueBlock = ParseBlock(stream, '[', ']');
                        var falseBlock = ParseBlock(stream, '[', ']');

                        output += ParseTemplate(projectItem, sourcePath, (bool)value ? trueBlock : falseBlock, context);
                    }
                    else
                    {
                        var block = ParseBlock(stream, '[', ']');
                        if (value != null)
                        {
                            if (block != null)
                            {
                                output += ParseTemplate(projectItem, sourcePath, block, value);
                            }
                            else
                            {
                                output += value.ToString();
                            }
                        }
                    }

                    return true;
                }
            }

            return false;
        }

    }
}
