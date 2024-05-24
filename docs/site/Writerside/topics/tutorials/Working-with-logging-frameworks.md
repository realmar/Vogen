# Working with logging frameworks

In this tutorial, we'll see how to log instances of value objects.

## Writing to the console

In a new console application, add a NuGet reference to `Vogen`, and create:

```c#
[ValueObject<int>] 
public readonly partial struct Age 
{
}
```

Now, create an instance and write it to the console:

```c#
var age = Age.From(42);
Console.WriteLine("Age is " + age);
```

As expected, you'll see:

```Bash
Age is 42
```

Vogen generates a default `ToString` method that looks like this:

```C#
public override string ToString() =>_isInitialized 
    ? Value.ToString() 
    : "[UNINITIALIZED]";
```

If the value is uninitialized, for example, through deserialization, then it writes
uninitialized, otherwise, it uses the underlying type's `ToString` method.

[//]: # (TODO: expand on ToString, either here, or in another page, and/or another How-to article)

It is possible to override `ToString`—for example, you might want to pad the value with zeroes. Add the following
method to `Age` above:

```C#
[ValueObject<int>]
public readonly partial struct Age
{
    public override string ToString() =>_isInitialized 
        ? Value.ToString("D4") 
        : "[UNINITIALIZED]";    
}
```

That will now print:

```Bash
Age is 0042
```

## Using the default logging framework

Next, we'll log values out using the default .NET logging framework.

Add NuGet references to:
* `Microsoft.Extensions.Logging` 
* `Microsoft.Extensions.Logging.Console`

Next, add a namespace reference to `Microsoft.Extensions.Logging` and create an instance of a logger that writes
to the console:

```c#
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
});

ILogger logger = loggerFactory.CreateLogger<Program>();
```

Now, change the `Console.WriteLine` to:

```C#
logger.LogInformation($"Age is {Age}", age);
```

The output is something like this:

```Bash
info: Program[0]
      Age is 0042
```

So far, we've used the standard .NET logger to write to a console, and we've written a value object using structured
logging.

Next, we'll look more at structured logging and switch to **Serilog**, a common choice for .NET.

## Structured logging with Serilog

<note>
Structured logging means that you're logging events as data structures rather than formatted text messages. 
With structured logging, you're essentially writing log events as structured data objects rather than plain text. 
The structured data objects can then be stored, queried, and processed in ways that aren’t possible with text logs.
</note>

Structure logging is supported by the default .NET logger.
The structured properties are inside 'index placeholders', for example, `{Age}` above.
The default logger supports structured logging.
However, it can't fully use structured data.
For this, we'll use Serilog.

* create a new console app
* add NuGet packages for `Vogen`, `Serilog`, and `Serilog.Sinks.Console`
* create the `Age` value object from above

Add the following initialization:

```C#
using Serilog;
using Vogen;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();


Age age = Age.From(42);
Log.Information("Age is {Age}", age);
```

Run it again, and you'll see something like this:

```Bash
[06:24:32 INF] Age is 0042
```

Let's create a bit more structure. Add a `Person` object:

```C#
public record Person(string Name, Age Age)
{
}
```

Replace what is logged with:

```C#
Log.Information("Person is {Person}", new Person("Joe", age));
```

You'll see:

```c#
[06:26:32 INF] Person is Person { Name = Joe, Age = 0042 }
```

In Serilog and some other .NET logging libraries that support structured logging, the `@` character in a placeholder 
is called the destructuring operator.
When you use the @ character before a placeholder, it tells the logging system to serialize the object completely 
instead of just calling ToString().
This means the complete state of the object is logged,
which can give you much more useful information for complex objects.

In our simple example, changing the logging line to:

```C#
Log.Information("Person is {@Person}", new Person("Joe", age));
```

Results in:
```Bash
Person is {"Name": "Joe", "Age": {"Value": 42, "$type": "Age"}, "$type": "Person"}
```

You can see that `Name` is written as: `Joe`.
But `Age` is written as: `{"Value": 42, "$type": "Age"}`

You might find this output too detailed for value objects and that it pollutes the logs.

We can tell Serilog to use the simpler format for value objects generated by Vogen.

Add the following `Destructure` line to the initialization:

```C#
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .Destructure.With(new VogenDestructuringPolicy())
    .CreateLogger();
```

And add this type to the project:

```C#
public class VogenDestructuringPolicy : IDestructuringPolicy
{
    public bool TryDestructure(
        object value, 
        ILogEventPropertyValueFactory propertyValueFactory, 
        out LogEventPropertyValue result)
    {
        if(value.GetType().GetCustomAttribute(
            typeof(ValueObjectAttribute)) is ValueObjectAttribute)
        {
            result = new ScalarValue(value.ToString());
            return true;
        }

        result = null;
        return false;
    }
}
```

Now, run again, and see that our value object `Age` is written more simply:

```Bash
Person is {"Name": "Joe", "Age": "0042", "$type": "Person"}
```

In this tutorial, we've seen how to log value objects to the console and to the default logger.
We've also seen how to use structured logging and how to customize the output of value objects written to Serilog.
