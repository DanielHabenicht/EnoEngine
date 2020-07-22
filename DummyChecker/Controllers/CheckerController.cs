﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using EnoCore.Models.Json;

namespace GamemasterChecker.Controllers
{
    [ApiController]
    [Route("/")]
    [Route("/service")]
    internal class CheckerController : Controller
    {
        [HttpPost]
        [Route("/")]
#pragma warning disable IDE0060
        public IActionResult Flag([FromBody] CheckerTaskMessage content)
#pragma warning restore IDE0060
        {
            return Ok("{ \"result\": \"OK\" }");
        }
        [HttpGet]
        [Route("/service")]
        public IActionResult Service()
        {
            return Ok(JsonSerializer.Serialize(new CheckerInfoMessage
            {
                ServiceName = "DummyChecker",
                FlagCount = 1,
                NoiseCount = 1,
                HavocCount = 1
            }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }
            ));
        }
    }
}
