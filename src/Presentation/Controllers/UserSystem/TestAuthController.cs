using DbApp.Domain.Services.UserSystem;
using Microsoft.AspNetCore.Mvc;

namespace DbApp.Presentation.Controllers.UserSystem;

/// <summary>
/// Test controller to verify authentication implementation.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TestAuthController : ControllerBase
{
    /// <summary>
    /// Test endpoint to verify the API is working.
    /// </summary>
    /// <returns>Test response</returns>
    [HttpGet("ping")]
    public ActionResult<object> Ping()
    {
        return Ok(new 
        { 
            message = "Authentication API is working", 
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        });
    }

    /// <summary>
    /// Test endpoint to verify Redis cache is working.
    /// </summary>
    /// <returns>Cache test response</returns>
    [HttpGet("test-cache")]
    public async Task<ActionResult<object>> TestCache([FromServices] DbApp.Domain.Services.UserSystem.ICacheService cacheService)
    {
        try
        {
            var testKey = "test_cache_key";
            var testValue = new { message = "Cache test", timestamp = DateTime.UtcNow };
            
            // Set cache value
            await cacheService.SetAsync(testKey, testValue, TimeSpan.FromMinutes(1));
            
            // Get cache value
            var cachedValue = await cacheService.GetAsync<object>(testKey);
            
            return Ok(new 
            { 
                success = true,
                original = testValue,
                cached = cachedValue,
                message = "Cache is working properly"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new 
            { 
                success = false,
                message = "Cache test failed",
                error = ex.Message
            });
        }
    }
}
