using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Xunit;

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

  [Fact]
  public void SoloBase_PoliticaEstrictaComoProd()
  {
    PasswordOptions opts = Bind(addDevelopment: false);
    Assert.Equal(8, opts.RequiredLength);
    Assert.True(opts.RequireDigit);
    Assert.True(opts.RequireUppercase);
    Assert.True(opts.RequireLowercase);
    Assert.True(opts.RequireNonAlphanumeric);
  }

  [Fact]
  public void ConOverrideDevelopment_PoliticaLaxaComoDev()
  {
    PasswordOptions opts = Bind(addDevelopment: true);
    Assert.Equal(6, opts.RequiredLength);
    Assert.False(opts.RequireDigit);
    Assert.False(opts.RequireUppercase);
    Assert.False(opts.RequireLowercase);
    Assert.False(opts.RequireNonAlphanumeric);
  }
}
