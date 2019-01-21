using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using MondBot.Master.Models;

namespace MondBot.Master.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PasteController : ControllerBase
    {
        private const string PasteDirectory = "pastes";

        private static readonly HashSet<char> HashCharacters = new HashSet<char>
        {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            'a', 'b', 'c', 'd', 'e', 'f', 'A', 'B', 'C', 'D', 'E', 'F'
        }; 

        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly string _baseDirectory;

        public PasteController(IHostingEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
            _baseDirectory = Path.Combine(Directory.GetCurrentDirectory(), PasteDirectory);
        }

        [HttpGet("{hash}")]
        public async Task<IActionResult> Get(string hash)
        {
            return Content(await Load(hash));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] PasteRequest paste)
        {
            return Content(await Store(paste.Content));
        }

        private async Task<string> Load(string hash)
        {
            if (string.IsNullOrEmpty(hash) || hash.Length < 4 || !hash.All(HashCharacters.Contains))
                return null;

            if (!Directory.Exists(_baseDirectory))
                return null;
                
            var directory = Path.Combine(_baseDirectory, hash.Substring(0, 2));

            if (!Directory.Exists(directory))
                return null;

            var path = Path.Combine(directory, hash);

            if (!System.IO.File.Exists(path))
                return null;

            using (var stream = System.IO.File.OpenRead(path))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return await reader.ReadToEndAsync();
            }
        }

        private async Task<string> Store(string content)
        {
            if (string.IsNullOrWhiteSpace(content) || content.Length >= short.MaxValue)
                return null;

            var sha1 = new SHA1CryptoServiceProvider();

            var contentBytes = Encoding.UTF8.GetBytes(content);
            var hash = BitConverter.ToString(sha1.ComputeHash(contentBytes)).Replace("-", "").ToLower();

            if (!Directory.Exists(_baseDirectory))
                Directory.CreateDirectory(_baseDirectory);

            var directory = Path.Combine(_baseDirectory, hash.Substring(0, 2));

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, hash);

            using (var stream = System.IO.File.OpenWrite(path))
            {
                await stream.WriteAsync(contentBytes, 0, contentBytes.Length);
            }

            return hash;
        }
    }
}
