// Punto de entrada nombrado para WebApplicationFactory<T> en los tests.
// Vive en un archivo aparte (los top-level statements de Program.cs van al
// namespace global) para que Enigma.Api.Program y Enigma.Web.Program no
// colisionen cuando un proyecto referencia ambos.
namespace Enigma.Api;

public partial class Program { }
