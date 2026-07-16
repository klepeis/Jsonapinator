using Microsoft.AspNetCore.Mvc;

namespace Jsonapinator.Sample.ConventionBased;

// Demonstrates a Guid-keyed resource -- convention-based mapping supports string/Guid/int/long
// ids, always serialized as a JSON string.
[ApiController]
[Route("print-runs")]
public class PrintRunsController : ControllerBase
{
    private static readonly List<PrintRun> PrintRuns =
    [
        new PrintRun { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), CopyCount = 500 },
    ];

    [HttpGet]
    public IEnumerable<PrintRun> GetAll() => PrintRuns;
}
