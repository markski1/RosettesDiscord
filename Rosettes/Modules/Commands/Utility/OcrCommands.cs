using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Discord;
using Discord.Interactions;
using Rosettes.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace Rosettes.Modules.Commands.Utility;

[CommandContextType(InteractionContextType.BotDm, InteractionContextType.PrivateChannel, InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.GuildInstall, ApplicationIntegrationType.UserInstall)]
public class OcrCommands : InteractionModuleBase<SocketInteractionContext>
{
    private const int MaxDownloadBytes = 10 * 1024 * 1024;
    private const long MaxPixels = 25_000_000;
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan OcrTimeout = TimeSpan.FromSeconds(30);
    private static readonly SemaphoreSlim OcrSlots = new(2, 2);

    [SlashCommand("ocr", "Read text from an attached image.")]
    public async Task Ocr([Summary("image", "Image to read.")] IAttachment image)
        => await ReadImageAsync(image.Url, (ulong)image.Size);

    [MessageCommand("OCR")]
    public async Task OcrMessage(IMessage message)
    {
        var attachment = message.Attachments.FirstOrDefault(item =>
            item.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true);
        string? imageUrl = attachment?.ProxyUrl ?? message.Embeds
            .Select(embed => embed.Image?.ProxyUrl ?? embed.Thumbnail?.ProxyUrl)
            .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));

        if (imageUrl is null)
        {
            attachment = message.Attachments.FirstOrDefault();
            imageUrl = attachment?.ProxyUrl;
        }

        if (imageUrl is null)
        {
            await RespondAsync("That message has no image to read.", ephemeral: true);
            return;
        }

        await ReadImageAsync(imageUrl, attachment is null ? null : (ulong)attachment.Size);
    }

    private async Task ReadImageAsync(string imageUrl, ulong? size)
    {
        await DeferAsync();

        if (size > MaxDownloadBytes)
        {
            await FollowupAsync("Please attach an image smaller than 10 MB.");
            return;
        }

        if (!await OcrSlots.WaitAsync(TimeSpan.FromSeconds(10)))
        {
            await FollowupAsync("OCR is busy right now. Please try again shortly.");
            return;
        }

        string path = Path.Combine(Path.GetTempPath(), $"rosettes-ocr-{Guid.NewGuid():N}.png");
        try
        {
            using var downloadTimeout = new CancellationTokenSource(DownloadTimeout);
            using var response = await Global.HttpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, downloadTimeout.Token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxDownloadBytes)
            {
                await FollowupAsync("Please attach an image smaller than 10 MB.");
                return;
            }

            await using var input = await response.Content.ReadAsStreamAsync(downloadTimeout.Token);
            using var bytes = new MemoryStream();
            byte[] buffer = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(buffer, downloadTimeout.Token)) != 0)
            {
                if (bytes.Length + read > MaxDownloadBytes)
                {
                    await FollowupAsync("Please attach an image smaller than 10 MB.");
                    return;
                }

                await bytes.WriteAsync(buffer.AsMemory(0, read), downloadTimeout.Token);
            }

            bytes.Position = 0;
            var options = new DecoderOptions { MaxFrames = 1, SkipMetadata = true };
            var info = Image.Identify(options, bytes);
            if (info is null)
            {
                await FollowupAsync("I couldn't read that image format.");
                return;
            }

            if ((long)info.Width * info.Height > MaxPixels)
            {
                await FollowupAsync("That image is too large to process.");
                return;
            }

            bytes.Position = 0;
            using var decodeTimeout = new CancellationTokenSource(OcrTimeout);
            using (var decoded = await Image.LoadAsync(options, bytes, decodeTimeout.Token))
            {
                if (Math.Max(decoded.Width, decoded.Height) <= 1600 &&
                    (long)decoded.Width * decoded.Height * 4 <= MaxPixels)
                    decoded.Mutate(context => context.Resize(decoded.Width * 2, decoded.Height * 2, KnownResamplers.Bicubic));

                await decoded.SaveAsPngAsync(path, new PngEncoder { SkipMetadata = true }, decodeTimeout.Token);
            }

            string result = await RunTesseractAsync(path);
            if (string.IsNullOrWhiteSpace(result))
            {
                await FollowupAsync("I couldn't find any text in that image.");
                return;
            }

            result = result.Trim();
            if (result.Length <= 1900 && !result.Contains("```", StringComparison.Ordinal))
            {
                await FollowupAsync($"```\n{result}\n```", allowedMentions: AllowedMentions.None);
            }
            else
            {
                await using var text = new MemoryStream(Encoding.UTF8.GetBytes(result));
                await FollowupWithFileAsync(text, "ocr.txt", text: "Extracted text:");
            }
        }
        catch (OperationCanceledException)
        {
            await FollowupAsync("OCR timed out. Please try a smaller image.");
        }
        catch (Win32Exception)
        {
            await FollowupAsync("OCR is unavailable because Tesseract is not installed on this server.");
        }
        catch (UnknownImageFormatException)
        {
            await FollowupAsync("I couldn't read that image format.");
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("ocr", ex.ToString());
            await FollowupAsync("Sorry, I couldn't read text from that image.");
        }
        finally
        {
            OcrSlots.Release();
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Global.GenerateErrorMessage("ocr", $"Could not delete temporary image: {ex.Message}");
            }
        }
    }

    private static async Task<string> RunTesseractAsync(string path)
    {
        var startInfo = new ProcessStartInfo("tesseract")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(path);
        startInfo.ArgumentList.Add("stdout");
        startInfo.ArgumentList.Add("-l");
        startInfo.ArgumentList.Add("eng+spa");
        startInfo.ArgumentList.Add("--oem");
        startInfo.ArgumentList.Add("1");
        startInfo.Environment["OMP_THREAD_LIMIT"] = "1";

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("Tesseract did not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(OcrTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw;
        }

        string error = await errors;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Tesseract exited with code {process.ExitCode}: {error}");

        return await output;
    }
}
