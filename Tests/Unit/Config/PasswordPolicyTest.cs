using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace Enigma.Test.Config;

public class PasswordPolicyTest
{
  private static string ServerDir
  {
    get
    {
      DirectoryInfo? current = new(AppContext.BaseDirectory);
      while (current != null)
      {
        if (File.Exists(Path.Combine(current.FullName, "Enigma.slnx")))
        {
          return Path.Combine(current.FullName, "Server");
        }
        current = current.Parent;
      }
      throw new InvalidOperationException("No se encontró la raíz del repo (Enigma.slnx).");
    }
  }

  private static PasswordOptions Bind(bool addDevelopment)
  {
    IConfigurationBuilder cfg = new ConfigurationBuilder()
        .SetBasePath(ServerDir)
        .AddJsonFile("appsettings.json", optional: false);
    if (addDevelopment)
    {
      cfg.AddJsonFile("appsettings.Development.json", optional: false);
    }
    PasswordOptions opts = new();
    cfg.Build().GetSection("Identity:Password").Bind(opts);
    return opts;
  }

  [Test]
  public void SoloBase_PoliticaEstrictaComoProd()
  {
    PasswordOptions opts = Bind(addDevelopment: false);
    Assert.That(opts.RequiredLength, Is.EqualTo(8));
    Assert.That(opts.RequireDigit, Is.True);
    Assert.That(opts.RequireUppercase, Is.True);
    Assert.That(opts.RequireLowercase, Is.True);
    Assert.That(opts.RequireNonAlphanumeric, Is.True);
  }

  [Test]
  public void ConOverrideDevelopment_PoliticaLaxaComoDev()
  {
    PasswordOptions opts = Bind(addDevelopment: true);
    Assert.That(opts.RequiredLength, Is.EqualTo(6));
    Assert.That(opts.RequireDigit, Is.False);
    Assert.That(opts.RequireUppercase, Is.False);
    Assert.That(opts.RequireLowercase, Is.False);
    Assert.That(opts.RequireNonAlphanumeric, Is.False);
  }
}
