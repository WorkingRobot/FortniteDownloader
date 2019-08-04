# FortniteDownloader
## Usage

### Authentication

    var auth = new Authorization("ninja@gmail.com", "p@ssword");
    await auth.Login();
### Downloading

    using (var downloader = new Downloader(auth))
    using (var stream = await downloader.OpenFile("FortniteGame/Content/Movies/Onboarding_Appended_Intro.mp4"))
    using (var outStream = File.OpenWrite("out.mp4"))
        await stream.CopyToAsync(outStream);
#### Downloading with a Progress Bar
    using (var downloader = new Downloader(auth))
    using (var stream = await downloader.OpenFile("FortniteGame/Content/Movies/Onboarding_Appended_Intro.mp4"))
    using (var outStream = File.OpenWrite("out.mp4"))
    {
        double progress = 0;
        byte[] buffer = new byte[1024 * 1024]; // 1 MB
        int read;
        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
        {
            await outStream.WriteAsync(buffer, 0, read).ConfigureAwait(false);

            // report progress back
            progress += (double)read / stream.Length * 100;
            Console.Write($"\r{new string('=', (int)progress / 2)}{new string('-', (int)(100 - progress) / 2)} {Math.Round(progress, 2)}%");
        }
    }

Just look into the files for documentation if necessary. I might add some later.