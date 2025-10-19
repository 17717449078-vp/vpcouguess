using System.Xml.Linq;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        app.UseStaticFiles();
       
        // �Ѷ����������
        var countryData = new Dictionary<string, (int min, int max, string[] countries)>
        {
            ["hard"] = (8000000, 20000000, new[] { "������", "������", "�������", "���", "��¡��" }),
            ["medium"] = (2000000, 20000000, new[] { "�Ĵ�����", "����", "���ױ���", "̹ɣ����", "����͢" }),
            ["easy"] = (200000000, 2000000000, new[] { "��������", "�ͻ�˹̹", "ӡ��������", "����", "����" })
        };

        // ��Ϸ��ʼ�������ع��Һ��˿ڷ�Χ
        app.MapPost("/api/start", async (HttpContext context) =>
        {
            var req = await context.Request.ReadFromJsonAsync<StartRequest>();
            var difficulty = req?.Difficulty?.ToLower() ?? "easy";
            if (!countryData.ContainsKey(difficulty))
                return Results.BadRequest("�Ѷ���Ч");

            var (min, max, countries) = countryData[difficulty];
            var rnd = new Random();
            var country = countries[rnd.Next(countries.Length)];

            return Results.Ok(new { country, min, max });
        });

        // ����API����·������Ϊ /api/answer
        app.MapPost("/api/answer", async (HttpContext context) =>
        {
            var request = await context.Request.ReadFromJsonAsync<AskRequest>();
            if (request is null || string.IsNullOrWhiteSpace(request.Question) || string.IsNullOrWhiteSpace(request.Country))
                return Results.BadRequest("����͹��Ҳ���Ϊ��");

            // DeepSeek/OpenAI API ����
            var apiKey = "sk-203ed4a6b7b34983aba9e01b35c4b8c9"; // TODO: �滻Ϊ�����Կ
            var apiUrl = "https://api.deepseek.com/v1/chat/completions"; // ����DeepSeek����OpenAI

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var prompt = $"����һ��ֻ���á��ǡ��򡰷񡱻ش���������֡���������Ĺ�����{request.Country}���˿���{request.Min}��{request.Max}֮�䡣�����������ҵ���Ϣ��ֻ�á��ǡ��򡰷����ش��û������⡣";

            var payload = new
            {
                model = "deepseek-chat", // �滻Ϊʵ��ģ����
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
                return Results.Problem("API����ʧ��");

            var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
            var answer = result?.choices?.FirstOrDefault()?.message?.content?.Trim() ?? "�޷���ȡ�ش�";

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
