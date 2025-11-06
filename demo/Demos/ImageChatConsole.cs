using System.Text.RegularExpressions;
using OllamaSharp;
using Spectre.Console;

namespace OllamaApiConsole.Demos;

public partial class ImageChatConsole(IOllamaApiClient ollama) : OllamaConsole(ollama)
{
	public override async Task Run()
	{
		AnsiConsole.Write(new Rule("Image chat").LeftJustified());
		AnsiConsole.WriteLine();

		Ollama.SelectedModel = await SelectModel("Select a model you want to chat with:");

		if (!string.IsNullOrEmpty(Ollama.SelectedModel))
		{
			var keepChatting = true;
			var systemPrompt = ReadInput($"Define a system prompt [{HintTextColor}](optional)[/]");

			do
			{
				AnsiConsole.MarkupLine("");
				AnsiConsole.MarkupLine($"You are talking to [{AccentTextColor}]{Ollama.SelectedModel}[/] now.");
				AnsiConsole.MarkupLine($"[{HintTextColor}]To send an image, simply enter its full filename like \"[{AccentTextColor}]c:/image.jpg[/]\"[/]");
				WriteChatInstructionHint();

				var chat = new Chat(Ollama, systemPrompt) { Think = Think };
				chat.OnThink += (sender, thoughts) => AnsiConsole.MarkupInterpolated($"[{AiThinkTextColor}]{thoughts}[/]");

				string message;

				do
				{
					AnsiConsole.WriteLine();
					message = ReadInput();

					if (message.Equals(EXIT_COMMAND, StringComparison.OrdinalIgnoreCase))
					{
						keepChatting = false;
						break;
					}

					if (message.Equals(TOGGLETHINK_COMMAND, StringComparison.OrdinalIgnoreCase))
					{
						ToggleThink();
						keepChatting = true;
						chat.Think = Think;
						continue;
					}

					if (message.Equals(START_NEW_COMMAND, StringComparison.OrdinalIgnoreCase))
					{
						keepChatting = true;
						break;
					}

					var imagePaths = WindowsFileRegex().Matches(message).Where(m => !string.IsNullOrEmpty(m.Value))
						.Union(UnixFileRegex().Matches(message).Where(m => !string.IsNullOrEmpty(m.Value)))
						.Select(m => m.Value)
						.ToArray();

					if (imagePaths.Length > 0)
					{
						byte[][] imageBytes;

						try
						{
							imageBytes = imagePaths.Select(File.ReadAllBytes).ToArray();
						}
						catch (IOException ex)
						{
							AnsiConsole.MarkupLineInterpolated($"Could not load your {(imagePaths.Length == 1 ? "image" : "images")}:");
							AnsiConsole.MarkupLineInterpolated($"[{ErrorTextColor}]{Markup.Escape(ex.Message)}[/]");
							AnsiConsole.MarkupLine("Please try again");
							continue;
						}

						// remove paths from the message
						foreach (var path in imagePaths)
							message = message.Replace(path, "");

						message += Environment.NewLine + Environment.NewLine + $"(the user attached {imagePaths.Length} {(imagePaths.Length == 1 ? "image" : "images")})";

						foreach (var consoleImage in imageBytes.Select(bytes => new CanvasImage(bytes)))
						{
							consoleImage.MaxWidth = 40;
							AnsiConsole.Write(consoleImage);
						}

						AnsiConsole.WriteLine();
						if (imagePaths.Length == 1)
							AnsiConsole.MarkupLine($"[{HintTextColor}]The image was scaled down for the console only, the model gets the full version.[/]");
						else
							AnsiConsole.MarkupLine($"[{HintTextColor}]The images were scaled down for the console only, the model gets full versions.[/]");
						AnsiConsole.WriteLine();

						bool first = true;
						var sw = System.Diagnostics.Stopwatch.StartNew();
						await foreach (var answerToken in chat.SendAsync(message, imageBytes))
						{
							if (first)
							{
								first = false;
								var el2 = sw.Elapsed;
								AnsiConsole.MarkupLine($"[{HintTextColor}]Start of response: {(int)el2.TotalMinutes:00}:{el2.Seconds:00}.{el2.Milliseconds / 10:00}[/]");
							}
							first = false;
							AnsiConsole.MarkupInterpolated($"[{AiTextColor}]{answerToken}[/]");
						}
						var el = sw.Elapsed;
						AnsiConsole.WriteLine();
						AnsiConsole.MarkupLine($"[{HintTextColor}]End of response: {(int)el.TotalMinutes:00}:{el.Seconds:00}.{el.Milliseconds / 10:00}[/]");
					}
					else
					{
						bool first = true;
						var sw = System.Diagnostics.Stopwatch.StartNew();
						await foreach (var answerToken in chat.SendAsync(message))
						{
							if (first)
							{
								first = false;
								var el2 = sw.Elapsed;
								AnsiConsole.MarkupLine($"[{HintTextColor}]Start of response: {(int)el2.TotalMinutes:00}:{el2.Seconds:00}.{el2.Milliseconds / 10:00}[/]");
							}
							AnsiConsole.MarkupInterpolated($"[{AiTextColor}]{answerToken}[/]");
						}
						var el = sw.Elapsed;
						AnsiConsole.WriteLine();
						AnsiConsole.MarkupLine($"[{HintTextColor}]End of response: {(int)el.TotalMinutes:00}:{el.Seconds:00}.{el.Milliseconds / 10:00}[/]");
					}
				} while (!string.IsNullOrEmpty(message));
			} while (keepChatting);
		}
	}

	/// <summary>
	/// https://stackoverflow.com/a/24703223/704281
	/// </summary>
	[GeneratedRegex("\\b[a-zA-Z]:[\\\\/](?:[^<>:\"/\\\\|?*\\n\\r]+[\\\\/])*[^<>:\"/\\\\|?*\\n\\r]+\\.\\w+\\b")]
	private static partial Regex WindowsFileRegex();

	/// <summary>
	/// https://stackoverflow.com/a/169021/704281
	/// </summary>
	[GeneratedRegex("(.+)\\/([^\\/]+)")]
	private static partial Regex UnixFileRegex();
}
