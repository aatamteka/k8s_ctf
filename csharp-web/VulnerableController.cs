using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;

namespace VulnerableApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class VulnerableController : ControllerBase
    {
        private readonly ILogger<VulnerableController> _logger;

        public VulnerableController(ILogger<VulnerableController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new
            {
                message = "C# Web App - JSON Processing Service",
                usage = "POST JSON data to /vulnerable/process",
                hint = "This endpoint deserializes JSON with type information",
                example = new {
                    data = "{\"name\":\"test\",\"value\":123}"
                }
            });
        }

        [HttpPost("process")]
        public IActionResult ProcessData([FromBody] ProcessRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Data))
                {
                    return BadRequest(new { error = "Data field is required" });
                }

                _logger.LogInformation("Processing JSON data...");

                // CRITICAL VULNERABILITY: Insecure JSON deserialization with TypeNameHandling
                // This allows arbitrary code execution through gadget chains
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All,
                    SerializationBinder = new CustomBinder() // Allow dangerous types
                };

                var obj = JsonConvert.DeserializeObject(request.Data, settings);

                return Ok(new
                {
                    message = "Data processed successfully",
                    result = obj?.ToString() ?? "null"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing data");
                return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
            }
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }
    }

    public class ProcessRequest
    {
        public string Data { get; set; } = string.Empty;
    }

    // Custom binder that allows deserialization of System.Diagnostics.Process
    public class CustomBinder : Newtonsoft.Json.Serialization.ISerializationBinder
    {
        public Type BindToType(string? assemblyName, string typeName)
        {
            return Type.GetType($"{typeName}, {assemblyName}") ?? typeof(object);
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            assemblyName = serializedType.Assembly.FullName;
            typeName = serializedType.FullName;
        }
    }

    // Vulnerable gadget class - Command property executes arbitrary commands
    public class CommandExecutor
    {
        private string _command = string.Empty;

        public string Command
        {
            get => _command;
            set
            {
                _command = value;
                if (!string.IsNullOrEmpty(_command))
                {
                    // Execute command when property is set during deserialization
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{_command.Replace("\"", "\\\"")}\"",
                        RedirectStandardOutput = false,
                        RedirectStandardError = false,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    try
                    {
                        // Start process without waiting - allows reverse shells to work
                        Process.Start(processInfo);
                    }
                    catch
                    {
                        // Silently fail
                    }
                }
            }
        }
    }
}
