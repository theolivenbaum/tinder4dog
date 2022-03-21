using System.Security.Cryptography;
using System.Net;
using System.IO;
using System.Globalization;
using Mosaik.Core;
using UID;
using System.Text.Json;

Console.WriteLine("Hello, World!");

var rng = new Random(42);

var profilesPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "data", "profiles"));
var imagesPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "data", "images"));
var namesPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "data", "names", "names.txt"));
var universitiesPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "data", "universities", "world_universities_and_domains.json"));
var jobsPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "data", "jobs", "jobs.txt"));
var finalPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "data", "profiles.json"));
var finalImagesPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "app", "images", "photos"));

Directory.CreateDirectory(profilesPath);
Directory.CreateDirectory(imagesPath);
Directory.CreateDirectory(finalImagesPath);

var emojiMap = new Dictionary<string, string[]>()
{
    ["Travel"] = new[] { "✈️", "🌍", "🌎", "🌏", "⛵", "🚢" },
    ["Zombie"] = new[] { "🧟", "☠️", "💀", "😵" },
    ["beer"] = new[] { "🍺", "🍻" },
    ["trailblazer"] = new[] { "👟", "🚵‍", "🚵", "🏔" },
    ["Tv"] = new[] { "📺", "🍿", "🎬" },
    ["nerd"] = new[] { "🤓" },
    ["Music"] = new[] { "🎵", "🎶", "🎷", "🎸", "🎺", "🎹", "🎧" },
    ["Bacon"] = new[] { "🥓", "🔥" },
    ["Explorer"] = new[] { "🌏", "⛵" },
    ["Thinker"] = new[] { "🤔", "🤯" },
    ["ninja"] = new[] { "🥷", "⚔️" },
    ["coffee"] = new[] { "☕", "🍵" },
    ["Wine"] = new[] { "🍷", "🍾" },
    ["Gamer"] = new[] { "🕹", "🎮" },
};


if (args[0] == "images")
{
    var count = int.Parse(args[1]);
    await DownloadImages(count);
}
else if (args[0] == "profiles")
{
    var count = int.Parse(args[1]);
    await GetProfilesAsync(count);
}
else if (args[0] == "merge")
{
    await MergeProfilesAsync();
}

async Task MergeProfilesAsync()
{
    var namesPerGender = File.ReadAllLines(namesPath)
                    .Select(n => n.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries))
                    .Where(d => d.Length == 2)
                    .SelectMany(n => n)
                    .Select((d, i) => (name: d, gender: i % 2 == 0 ? "male" : "female"))
                    .GroupBy(g => g.gender)
                    .ToDictionary(g => g.Key, g => g.Select(g => g.name).ToArray());
    var profiles = Directory.GetFiles(profilesPath, "*.txt");
    var images = Directory.GetFiles(imagesPath, "*.jpg");


    var imagesPerBreed = images.Select(f => (path: f, breed: Path.GetFileNameWithoutExtension(f).Split(new char[] { '_' }).First()))
                               .GroupBy(f => f.breed)
                               .ToDictionary(g => g.Key, g => g.Select(g => g.path).ToList());
    
    var breeds = imagesPerBreed.Keys.ToList();

    var universities = JsonSerializer.Deserialize<University[]>(File.ReadAllText(universitiesPath));

    var numberOfProfiles = Math.Min(Math.Min(profiles.Length, images.Length), namesPerGender.Sum(g => g.Value.Length));
    var jobs = File.ReadAllLines(jobsPath);


    var final = new List<Profile>();
    int index = 0;

    foreach (var (gender, names) in namesPerGender)
    {
        foreach (var name in names.Take(150))
        {
            var imgFinal = new List<string>();
            while (imagesPerBreed.Count > 0)
            {
                var breed = breeds[rng.Next(breeds.Count)];
                var breedImages = imagesPerBreed[breed];
                if(breedImages.Count > 0)
                {
                    var toTake = Math.Min(4, 1 + rng.Next(breedImages.Count - 1));
                    var selectedImages = breedImages.Take(toTake).ToArray();
                    int c = 0;
                    foreach(var img in selectedImages)
                    {
                        breedImages.Remove(img);
                        var profileImage = $"profile_{index}_{c}.jpg";
                        File.Copy(img, Path.Combine(finalImagesPath, profileImage), true);
                        imgFinal.Add($"images/photos/{profileImage}");
                        c++;
                    }
                    break;
                }
                else
                {
                    imagesPerBreed.Remove(breed);
                    breeds.Remove(breed);
                }
            }
            if (imgFinal.Count == 0) break;
            var profile = profiles[index];
            var jobName = jobs[index % jobs.Length];

            final.Add(new Profile()
            {
                age = (int)(rng.NextDouble() * rng.NextDouble() * 30) + 1,
                name = name,
                description = Emojify(File.ReadAllText(profile).Replace("Alcohol", "Wine")),
                distance = (int)(rng.NextDouble() * rng.NextDouble() * 500) * 5,
                images = imgFinal.ToArray(),
                id = index,
                job = jobName,
                university = (jobName == "Teacher" || jobName == "Student" || rng.NextDouble() < 0.2) ? universities[rng.Next(universities.Length)].name : ""
            });

            index++;
        }
        File.WriteAllText(finalPath, JsonSerializer.Serialize(final.OrderBy(d => d.id).ToArray(), new JsonSerializerOptions() { WriteIndented = true }));
    }
}

string Emojify(string v)
{
    int count = rng.Next(5) + 1;
    foreach (var (key, candidates) in emojiMap)
    {
        if (v.Contains(key) && rng.NextDouble() > 0.3)
        {
            var emoji = candidates[rng.Next(candidates.Length)];
            v = v.Replace(key, emoji + " " + key);
            count--;
        }
        if (count == 0) break;
    }
    return v;
}

async Task GetProfilesAsync(int count)
{
    var client = new HttpClient();
    var cts = new CancellationTokenSource();
    long success = Directory.EnumerateFiles(profilesPath, "*.txt").Count();

    await Parallel.ForEachAsync(Enumerable.Range(0, count * 100), cts.Token, async (i, ct) =>
    {
        if (ct.IsCancellationRequested) return;

        var response = await client.GetAsync("https://www.twitterbiogenerator.com/generate");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var hash = SHA256.HashData(bytes);
        var b64 = Convert.ToBase64String(hash).Replace("+", "").Replace("/", "").Replace("=", "").Replace("-", "");
        var fn = Path.Combine(profilesPath, $"{b64}.txt");
        if (File.Exists(fn)) return;
        try
        {
            await using var file = File.OpenWrite(fn);
            await file.WriteAsync(bytes);
        }
        catch
        {
            return; //ignore
        }
        Console.WriteLine($"Downloaded {i} to {fn}");
        if (Interlocked.Increment(ref success) > count)
        {
            cts.Cancel();
        }
    });

}

async Task DownloadImages(int count)
{
    var client = new HttpClient();

    var cts = new CancellationTokenSource();

    long success = Directory.EnumerateFiles(imagesPath, "*.jpg").Count();

    await Parallel.ForEachAsync(Enumerable.Range(0, count * 100), cts.Token, async (i, ct) =>
    {
        if (ct.IsCancellationRequested) return;

        var response = await client.GetAsync("https://dog.ceo/api/breeds/image/random");
        if (!response.IsSuccessStatusCode) return;
        var responseJson = JsonSerializer.Deserialize<DogApiResponse>(await response.Content.ReadAsStringAsync());

        if (responseJson.status != "success") return;
        var responseImage = await client.GetAsync(responseJson.message);


        if (!responseImage.IsSuccessStatusCode) return;

        var breed = responseJson.message.Split(new char[] { '/' })[^2];

        var bytes = await responseImage.Content.ReadAsByteArrayAsync();
        var hash = SHA256.HashData(bytes);
        var b64 = Convert.ToBase64String(hash).Replace("+", "_").Replace("/", "-").Replace("=", "");
        var fn = Path.Combine(imagesPath, $"{breed}_{b64}.jpg");
        if (File.Exists(fn)) return;
        try
        {
            await using var file = File.OpenWrite(fn);
            await file.WriteAsync(bytes);
        }
        catch
        {
            return; //ignore
        }
        Console.WriteLine($"Downloaded {success} from {responseJson.message} to {fn}");
        if (Interlocked.Increment(ref success) > count)
        {
            cts.Cancel();
        }
    });
}
