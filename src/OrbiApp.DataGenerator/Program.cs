using OrbiApp.DataGenerator;

try
{
    var options = GeneratorOptions.Parse(args);
    var generator = new OrbiDataGenerator(options);
    await generator.RunAsync();
    return 0;
}
catch (HelpRequestedException)
{
    GeneratorOptions.PrintHelp();
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"ERROR: {exception.Message}");
    return 1;
}
