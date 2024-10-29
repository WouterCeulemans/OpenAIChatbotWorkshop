using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Files;

namespace OpenAIChatbot.Web.Controllers
{
    [ApiController]
    [Route("api/files")]
    public class FileApiController : Controller
    {
        private readonly OpenAIFileClient _fileClient;

        public FileApiController(AzureOpenAIClient openAIClient)
        {
            _fileClient = openAIClient.GetOpenAIFileClient();
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFiles(IFormFileCollection files, CancellationToken cancellationToken)
        {
            if (files.Count == 0)
            {
                return BadRequest("No files to upload");
            }

            var fileResults = new Dictionary<string, string>();
            foreach (IFormFile file in files)
            {
                await using var stream = file.OpenReadStream();
                var fileUploadResult = await _fileClient.UploadFileAsync(stream, file.FileName, FileUploadPurpose.Assistants, cancellationToken);
                fileResults.Add(file.FileName, fileUploadResult.Value.Id);
            }

            return Ok(fileResults);
        }
    }
}