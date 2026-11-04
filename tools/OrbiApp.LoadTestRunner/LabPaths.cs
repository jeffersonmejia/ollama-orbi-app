namespace OrbiApp.LoadTestRunner;

public static class LabPaths
{
    public static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OrbiApp.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Ejecuta el runner desde la raíz del repositorio Orbi.");
    }
}
