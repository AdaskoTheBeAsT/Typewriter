namespace Repro;

public class Factory<T> : Factory<T, object>
{
}

public class Factory<T, TResult>
{
}

public class Factory : Factory<string>
{
}
