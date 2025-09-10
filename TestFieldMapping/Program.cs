using System;
using Mapster;

public record TestRecord(string Name, string Description);

public class TestClass
{
    public string Name;
    public string Description;
}

class Program
{
    static void Main()
    {
        var record = new TestRecord("Test Name", "Test Description");
        var testClass = record.Adapt<TestClass>();
        
        Console.WriteLine($"Mapped to fields - Name: {testClass.Name}, Description: {testClass.Description}");
        
        if (testClass.Name == "Test Name" && testClass.Description == "Test Description")
        {
            Console.WriteLine("SUCCESS: Mapster successfully mapped record properties to class fields!");
        }
        else
        {
            Console.WriteLine("FAILURE: Mapster failed to map to fields");
        }
    }
}
