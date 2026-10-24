namespace Repro;

public interface IBaseContract<T>
{
    T Value { get; }
}

public interface IFactoryContract<T> : IBaseContract<T>
{
}

public interface IFactoryContract<T, TResult> : IBaseContract<T>
{
    TResult Result { get; }
}

public class Factory<T> : Factory<T, int>, IFactoryContract<T>
{
    public T Item { get; set; } = default!;
}

public class Factory<T, TResult> : IFactoryContract<T, TResult>
{
    public T Value => default!;

    public TResult Result => default!;
}

public class Factory : Factory<string>
{
    public IFactoryContract<string> Contract { get; set; } = default!;

    public IReadOnlyList<int> Numbers { get; set; } = [];
}
