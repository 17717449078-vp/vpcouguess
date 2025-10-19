using System.Xml.Linq;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        app.UseStaticFiles();
       
        // 难度与国家数据
        var countryData = new Dictionary<string, (int min, int max, string[] countries)>
        {
            ["hard"] = (8000000, 20000000, new[] { "葡萄牙", "阿联酋", "多美尼加", "瑞典", "布隆迪" }),
            ["medium"] = (2000000, 20000000, new[] { "澳大利亚", "韩国", "哥伦比亚", "坦桑尼亚", "阿根廷" }),
            ["easy"] = (200000000, 2000000000, new[] { "尼日利亚", "巴基斯坦", "印度尼西亚", "美国", "巴西" })
        };

        // 游戏初始化，返回国家和人口范围
        app.MapPost("/api/start", async (HttpContext context) =>
        {
            var req = await context.Request.ReadFromJsonAsync<StartRequest>();
            var difficulty = req?.Difficulty?.ToLower() ?? "easy";
            if (!countryData.ContainsKey(difficulty))
                return Results.BadRequest("难度无效");

            var (min, max, countries) = countryData[difficulty];
            var rnd = new Random();
            var country = countries[rnd.Next(countries.Length)];

            return Results.Ok(new { country, min, max });
        });

        // 代理API请求，路径调整为 /api/answer
        app.MapPost("/api/answer", async (HttpContext context) =>
        {
            var request = await context.Request.ReadFromJsonAsync<AskRequest>();
            if (request is null || string.IsNullOrWhiteSpace(request.Question) || string.IsNullOrWhiteSpace(request.Country))
                return Results.BadRequest("问题和国家不能为空");

            // DeepSeek/OpenAI API 配置
            var apiKey = "sk-203ed4a6b7b34983aba9e01b35c4b8c9"; // TODO: 替换为你的密钥
            var apiUrl = "https://api.deepseek.com/v1/chat/completions"; // 假设DeepSeek兼容OpenAI

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var prompt = $"你是一个只能用“是”或“否”回答问题的助手。你心中想的国家是{request.Country}，人口在{request.Min}到{request.Max}之间。请根据这个国家的信息，只用“是”或“否”来回答用户的问题。";

            var payload = new
            {
                model = "deepseek-chat", // 替换为实际模型名
                messages = new[]
                {
                    new { role = "system", content = prompt },
                    new { role = "user", content = request.Question }
                },
                max_tokens = 20,
                temperature = 0.2
            };

            var response = await httpClient.PostAsJsonAsync(apiUrl, payload);
            if (!response.IsSuccessStatusCode)
                return Results.Problem("API请求失败");

            var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
            var answer = result?.choices?.FirstOrDefault()?.message?.content?.Trim() ?? "无法获取回答";

            return Results.Ok(new { answer });
        });

        app.MapFallbackToFile("htmlpage.html");

        app.Run();
    }
}

record StartRequest(string Difficulty);
record AskRequest(string Question, string Country, int Min, int Max);

public class OpenAIResponse
{
    public required Choice[] choices { get; set; }
    public class Choice
    {
        public Message? message { get; set; }
    }
    public class Message
    {
        public required string content { get; set; }
    }
}
