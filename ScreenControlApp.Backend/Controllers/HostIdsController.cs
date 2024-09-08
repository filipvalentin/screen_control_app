using Microsoft.AspNetCore.Mvc;
using ScreenControlApp.Backend.Services;

namespace ScreenControlApp.Backend.Controllers {
	[Route("api/[controller]")]
	[ApiController]
	public class HostIdsController : ControllerBase {
		[HttpGet]
		public IActionResult Get() {
			var result = HostIdGeneratorService.Get();
			return Ok(result);
		}
		[HttpDelete("$hostId")]
		public IActionResult Delete(string hostId) {
			HostIdGeneratorService.Return(hostId);
			return Ok();
		}
	}
}
