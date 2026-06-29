using System.Collections.Generic;

namespace App;

public sealed class TypewriterAttribute : System.Attribute
{
}

public class Box<T>
{
    public T Value { get; set; } = default!;
}

[Typewriter]
public class Model
{
    public Box<int> Value { get; set; } = new();

    public List<Box<int>> Boxes { get; set; } = [];

    public Dictionary<string, Box<int>> BoxByName { get; set; } = [];
}
