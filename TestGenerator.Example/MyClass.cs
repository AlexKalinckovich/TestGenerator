namespace TestGenerator.Example;

public interface IDependency
{
    int GetValue();
}

public class MyClass
{
    private readonly IDependency _dependency;
    public MyClass(IDependency dependency) => _dependency = dependency;
    public void DoWork() => Console.WriteLine(_dependency.GetValue());
}