using Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestionService.Data;
using QuestionService.services;
using RabbitMQ.Client;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace QuestionService.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class QuestionsController(QuestionDbContext db, IConnection connection, TagService tagService) : ControllerBase
    {
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateQuestion([FromBody] DTOs.CreateQuestionDto createQuestionDto)
        {            
            //var validTags = await db.Tags.Where(t => createQuestionDto.Tags.Contains(t.Slug)).ToListAsync();
            //lấy những phần tử có trong tập A nhưng KHÔNG có trong tập B
            //Lấy tất cả slug mà user gửi lên nhưng không có trong DB
            //var missingTags = createQuestionDto.Tags.Except(validTags.Select(x=>x.Slug).ToList()).ToList();

            //if(missingTags.Any())
            //{
            //    return BadRequest($"The following tags are invalid: {string.Join(", ", missingTags)}");
            //}

            if(!await tagService.AreTagsValidAsync(createQuestionDto.Tags))
                return BadRequest("Invalid tag");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = User.FindFirstValue("name");
            if (userId is null || name is null)
            {
                return BadRequest("Can not find user");
            }
            var question = new Models.Question
            {
                Title = createQuestionDto.Title,
                Content = createQuestionDto.Content,
                TagSlugs = createQuestionDto.Tags,
                AskerId = userId,
                AskerDisplayName = name,
                CreatedAt = DateTime.UtcNow
            };

            db.Questions.Add(question);
            await db.SaveChangesAsync();

            var channel = connection.CreateModel();
            var props = channel.CreateBasicProperties();
            props.Persistent = true;//giup service ofline luu tin nhan lai và send khi online
            channel.QueueDeclare(
                queue: Contracts.Contanst.QuestionCreatedQueue,
                durable: true, //false → queue không được lưu sau khi RabbitMQ restart; true → queue sẽ persist.Do bạn để false, tức là queue chỉ tồn tại trong memory.
                exclusive: false,//true → queue chỉ một client được sử dụng, và bị xóa khi client disconnect. false → nhiều client có thể sử dụng.
                autoDelete: false,// true: queue sẽ bị tự động xóa khi không còn consumer nào đăng ký. false: queue vẫn giữ lại.
                arguments: null
            );

            var temp = new QuestionCreated
            (
                question.Id,
                question.Content,
                question.Title,
                question.CreatedAt,
                question.TagSlugs
            );

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(temp));

            channel.BasicPublish(exchange: string.Empty,
                routingKey: Contracts.Contanst.QuestionCreatedQueue,
                mandatory: false,//false → nếu không có queue phù hợp → message sẽ bị mất; true → message sẽ trả về nếu không có queue phù hợp.
                basicProperties: props, // service ofline có thể mất điện đột ngột, khi đó message trong memory sẽ bị mất. Để tránh tình trạng này, ta đánh dấu message là persistent.
                body: body);

            return Created($"questions/{question.Id}", question);
        }

        [HttpGet]
        public async Task<IActionResult> GetQuestions(string? tag)
        {
            var query = db.Questions.AsQueryable();
            if(!string.IsNullOrEmpty(tag))
            {
                query = query.Where(q => q.TagSlugs.Contains(tag));
            }
            var questions =  await query.OrderByDescending(x=>x.CreatedAt).ToListAsync();
            return Ok(questions);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> DetailQuestion(string id)
        {
            var questions = await db.Questions.FindAsync(id);
            if (questions is null)
                return NotFound();

            await db.Questions.Where(x=>x.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x=> x.AnswerCount, x=>x.AnswerCount + 1));

            return Ok(questions);
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutQuestion(string id, DTOs.CreateQuestionDto dto)
        {
            var question = await db.Questions.FindAsync(id);
            if (question is null)
                return NotFound();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (question.AskerId != userId)
            {
                return Forbid();
            }

            //var validTags = await db.Tags.Where(t => dto.Tags.Contains(t.Slug)).ToListAsync();
            //lấy những phần tử có trong tập A nhưng KHÔNG có trong tập B
            //Lấy tất cả slug mà user gửi lên nhưng không có trong DB
            //var missingTags = dto.Tags.Except(validTags.Select(x => x.Slug).ToList()).ToList();

            //if (missingTags.Any())
            //{
            //    return BadRequest($"The following tags are invalid: {string.Join(", ", missingTags)}");
            //}
            if (!await tagService.AreTagsValidAsync(dto.Tags))
                return BadRequest("Invalid tag");

            question.Title = dto.Title;
            question.Content = dto.Content;
            question.TagSlugs = dto.Tags;
            question.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var channel = connection.CreateModel();
            channel.QueueDeclare(
                queue: Contanst.QuestionUpdatedQueue,
                durable: true, //false → queue không được lưu sau khi RabbitMQ restart; true → queue sẽ persist.Do bạn để false, tức là queue chỉ tồn tại trong memory.
                exclusive: false,//true → queue chỉ một client được sử dụng, và bị xóa khi client disconnect. false → nhiều client có thể sử dụng.
                autoDelete: false,// true: queue sẽ bị tự động xóa khi không còn consumer nào đăng ký. false: queue vẫn giữ lại.
                arguments: null
            );

            var temp = new QuestionUpdated
            (
                question.Id,
                question.Title,
                question.Content,
                question.TagSlugs.ToArray()
            );

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(temp));

            channel.BasicPublish(exchange: string.Empty,
                routingKey: Contanst.QuestionUpdatedQueue,
                mandatory: false,//false → nếu không có queue phù hợp → message sẽ bị mất; true → message sẽ trả về nếu không có queue phù hợp.
                basicProperties: null,
                body: body);

            return NoContent();
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteQuestion(string id)
        {
            var question = await db.Questions.FindAsync(id);
            if (question is null)
                return NotFound();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (question.AskerId != userId)
            {
                return Forbid();
            }
            db.Questions.Remove(question);
            await db.SaveChangesAsync();

            var channel = connection.CreateModel();
            channel.QueueDeclare(
                queue: Contanst.QuestionDeletedQueue,
                durable: true, //false → queue không được lưu sau khi RabbitMQ restart; true → queue sẽ persist.Do bạn để false, tức là queue chỉ tồn tại trong memory.
                exclusive: false,//true → queue chỉ một client được sử dụng, và bị xóa khi client disconnect. false → nhiều client có thể sử dụng.
                autoDelete: false,// true: queue sẽ bị tự động xóa khi không còn consumer nào đăng ký. false: queue vẫn giữ lại.
                arguments: null
            );

            var temp = new QuestionDeleted(question.Id);

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(temp));

            channel.BasicPublish(exchange: string.Empty,
                routingKey: Contanst.QuestionDeletedQueue,
                mandatory: false,//false → nếu không có queue phù hợp → message sẽ bị mất; true → message sẽ trả về nếu không có queue phù hợp.
                basicProperties: null,
                body: body);

            return NoContent();
        }

        [HttpGet("errors")]
        public IActionResult Errors(int code)
        {
            return code switch
            {
                400 => BadRequest(new { Message = "The request was invalid or cannot be served." }),
                401 => Unauthorized(new { Message = "Authentication is required and has failed or has not yet been provided." }),
                404 => NotFound(new { Message = "The resource you requested could not be found." }),
                500 => StatusCode(500, new { Message = "An unexpected error occurred on the server." }),
                _ => BadRequest(new { Message = "An unknown error occurred." })
            };
        }
    }
}
