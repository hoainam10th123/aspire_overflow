using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestionService.Data;

namespace QuestionService.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class TagsController(QuestionDbContext db) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetTags()
        {
            var tags = await db.Tags.OrderBy(x=>x.Name).ToListAsync();
            return Ok(tags);
        }
    }
}
