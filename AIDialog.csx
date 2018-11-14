#r "Newtonsoft.Json"

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using System.Configuration;

[Serializable]
public class AIDialog : IDialog<object>
{
    private string LastImageUrl;

    public Task StartAsync(IDialogContext context)
    {
        context.Wait(MessageReceivedAsync);

        return Task.CompletedTask;
    }

    private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
    {   
        var activity = await result;
        if (activity.Attachments.Any())
        {
            LastImageUrl = activity.Attachments[0].ContentUrl;
            await context.PostAsync("Thanks for the picture ;)");
        }
        
        if (string.IsNullOrEmpty(LastImageUrl))
        {
            await context.PostAsync("Sorry, I need an image to work with...");
        }
        else
        {
            ShowOptions(context);
        }
    }

    private void ShowOptions(IDialogContext context) {
        PromptDialog.Choice<string>(
                context,
                AfterChoiceAsync,
                new []{"Describe the picture", "Count people on the picture", "Who's in the picture", "Forget this image"},
                "What do you want to know about this image?",
                "Ops, I didn't get that...",
                5,
                promptStyle: PromptStyle.Auto);
    }

    public async Task AfterChoiceAsync(IDialogContext context, IAwaitable<string> argument)
    {
        var response = await argument;
        await context.PostAsync("Let me see...");
        switch (response)
        {
            case "Describe the picture":
                var (description, tags) = await InvokeComputerVisionAsync(LastImageUrl);
                await context.PostAsync($"I can see {description}, some words I would use to describe it are {tags}");
                break;
            case "Count people on the picture":
                await context.PostAsync((await InvokeFaceApiAsync(LastImageUrl)).Count().ToString());
                break;
            case "Who's in the picture":
                var people = await InvokeFaceApiAsync(LastImageUrl);
                var message = string.Join(", ", people.Select(s => $"a {s.Age} years old {s.Gender}{(s.Smile > .5 ? " smiling" : "")}"));
                await context.PostAsync($"I think there is {message}");
                break;
            case "Forget this image":
                LastImageUrl = null;
                await context.PostAsync($"Image? Which image? :S");
                return;
            default:
                await context.PostAsync("Not sure what you want from me...");
                break;
        }

        ShowOptions(context);
    }

    public async Task<(string description, string tags)> InvokeComputerVisionAsync(string imageUrl)
    {
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", ConfigurationManager.AppSettings["VisionApiKey"]);
            
            var url = "https://canadacentral.api.cognitive.microsoft.com/vision/v1.0/analyze?visualFeatures=Categories,Description&language=en";
            
            var response = await client.PostAsync(new Uri(url), new StringContent("{\"url\":\"" + imageUrl + "\"}", System.Text.Encoding.UTF8, "application/json"));
 
            var json = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
            //var description = json.description.captions[0].text;
            //var tags = string.Join(", ", json.description.tags);
            return (json.description.captions[0].text, string.Join(", ", json.description.tags));
        }
    }

    public async Task<IEnumerable<FaceAttributes>> InvokeFaceApiAsync(string imageUrl)
    {        
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", ConfigurationManager.AppSettings["FaceApiKey"]);
            
            var url = "https://canadacentral.api.cognitive.microsoft.com/face/v1.0/detect?returnFaceAttributes=age,gender,smile";
            
            var response = await client.PostAsync(new Uri(url), new StringContent("{\"url\":\"" + imageUrl + "\"}", System.Text.Encoding.UTF8, "application/json"));
 
            //return await response.Content.ReadAsStringAsync();
            var json = JsonConvert.DeserializeObject<List<Face>>(await response.Content.ReadAsStringAsync());
            
            return json.Select(s => s.FaceAttributes);
        }
    }
}

public class Face 
{
    public FaceAttributes FaceAttributes { get; set; }
}

public class FaceAttributes
{
    public double Age { get; set; }
    public double Smile { get; set; }
    public string Gender { get; set; }
}
