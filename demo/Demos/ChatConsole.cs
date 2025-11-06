using OllamaSharp;
using Spectre.Console;

namespace OllamaApiConsole.Demos;

public class ChatConsole(IOllamaApiClient ollama) : OllamaConsole(ollama)
{
	public override async Task Run()
	{
		AnsiConsole.Write(new Rule("Chat").LeftJustified());
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
				} while (!string.IsNullOrEmpty(message));
			} while (keepChatting);
		}
	}
}