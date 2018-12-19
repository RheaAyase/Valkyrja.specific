using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
using Discord;
using Discord.WebSocket;
using Remotion.Linq.Clauses;
using guid = System.UInt64;

namespace Botwinder.modules
{
	public class Recruitment : IModule
	{
		private class PropertySpecification
		{
			public int Order = 0;
			public bool Inline = false;
			public bool Optional = false;
			public string Label = null;
			public string[] Options = null;
			public string[] ValidValues = null;
			public int CharacterLimit = 10;
			public string Prefix = "";
			public string Suffix = "";

			public PropertySpecification(int order, bool inline, bool optional, string label, string[] options, string[] validValues = null, int characterLimit=10, string prefix = "", string suffix = "")
			{
				this.Order = order;
				this.Inline = inline;
				this.Optional = optional;
				this.Label = label;
				this.Options = options;
				this.ValidValues = validValues;
				this.CharacterLimit = characterLimit;
				this.Prefix = prefix;
				this.Suffix = suffix;
			}
		}

		//This is a hardcoded server specific feature, the code is one huge hack and nobody should even read it. It's disgusting!
		//Especially the way I treat this list...
		private readonly IEnumerable<PropertySpecification> Properties = new List<PropertySpecification>{
			new PropertySpecification(-1, false, true, "", new string[]{"-b", "--logo"}, null, 500),
			new PropertySpecification(0, false, false, "Name", new string[]{"-n", "--name"}, null, 30),
			new PropertySpecification(1, true, false, "Platforms", new string[]{"-p", "--platform"}, new string[]{"PC", "XBox", "PS4"}, 13),
			new PropertySpecification(2, true, false, "Timezones", new string[]{"-t", "--timezone"}, new string[]{"EU", "NA", "APAC"}, 14),
			new PropertySpecification(3, false, false, "Comms type", new string[]{"-c", "--comms"}, new string[]{"Discord", "TeamSpeak", "Mumble", "Ventrilo", "Steam"}),
			new PropertySpecification(4, true, true, "Squadron", new string[]{"-s", "--squadron"}, null, 30, "`", "`"),
			new PropertySpecification(5, true, true, "Squadron ID", new string[]{"-i", "--squadronId"}, null, 4, "`[", "]`"),
			new PropertySpecification(6, false, true, "Links", new string[]{"-l", "--link"}, null, 100),
			new PropertySpecification(7, false, false, "Description", new string[]{"-d", "--description"}, null, 500)
		};

		private readonly Regex CommandParamRegex = new Regex("--?\\w+\\s(?!--?\\w|$).*?(?=\\s--?\\w|$)", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
		private readonly Regex CommandOptionRegex = new Regex("--?\\w+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
		private readonly Regex CommandValueRegex = new Regex("\\w+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
		private readonly Regex UserIdRegex = new Regex("\\d+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

		private BotwinderClient Client;

		public Func<Exception, string, guid, Task> HandleException{ get; set; }
		public bool DoUpdate{ get; set; } = false;

		public List<Command> Init(IBotwinderClient iClient)
		{
			this.Client = iClient as BotwinderClient;
			List<Command> commands = new List<Command>();

// !recruitment
			Command newCommand = new Command("recruitment");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.IsHidden = true;
			newCommand.OnExecute += async e => {
				if( e.Server.Id != 89778537522802688 )
					return;

				string response = "```md\nCreate or modify your #recruitment post with the following properties:\n\n" +
				                  "[ -b ][ --logo        ] | Optional URL to your logo (up to 128px)\n" +
				                  "[ -n ][ --name        ] | Name of your playergroup (up to 30char)\n" +
				                  "[ -p ][ --platform    ] | Platform: PC, XBox or PS4\n" +
				                  "[ -t ][ --timezone    ] | Timezone: EU, NA or APAC\n" +
				                  "[ -c ][ --comms       ] | Comms type: Discord, TeamSpeak, Mumble, Ventrilo or Steam\n" +
				                  "[ -s ][ --squadron    ] | Name of the squadron (optional field up to 30char)\n" +
				                  "[ -i ][ --squadronId  ] | [ID] of the squadron (optional field = 4char)\n" +
				                  "[ -l ][ --link        ] | Links to your Inara or Discord (optional field up to 100char)\n" +
				                  "[ -d ][ --description ] | Up to 500 characters of group description.\n" +
				                  "\nNote: --link and --description can be text that supports [markdown](links)." +
				                  "\nExample: !recruitment --name The Elite -p PC PS4 XBox -t EU NA APAC -c Discord --link [Discord](https://discord.gg/elite) -d Weirdoes." +
				                  "\n```";

				if( string.IsNullOrEmpty(e.TrimmedMessage) )
				{
					await e.SendReplySafe(response);
					return;
				}

				List<IMessage> messages = new List<IMessage>();
				IMessageChannel channel = e.Server.Guild.GetTextChannel(523925543045955594);
				guid lastMessage = 523927539983581196;
				int downloadedCount = 0;
				do
				{
					IMessage[] downloaded = await channel.GetMessagesAsync(lastMessage, Direction.After, 100, CacheMode.AllowDownload).Flatten().ToArray();
					lastMessage = messages.FirstOrDefault()?.Id ?? 0;
					downloadedCount = downloaded.Length;
					if( downloaded.Any() )
						messages.AddRange(downloaded);
				} while( downloadedCount >= 100 && lastMessage > 0 );

				IMessage message = messages.FirstOrDefault(m => guid.TryParse(this.UserIdRegex.Match(m.Content).Value, out guid id) && id == e.Message.Author.Id);
				if( message != null )
				{
					//todo if( message.embed == newembed ) return; ~ do not just bump the post without changing anything.
					//todo support modifying a single property...
					//await (message as SocketUserMessage).ModifyAsync(m => ModifyRecruitmentEmbed(e, m));
					//await e.SendReplySafe("All done.");
					//return;
					await message.DeleteAsync();
				}

				object embed = GetRecruitmentEmbed(e);
				if( embed != null )
				{
					if( embed is Embed )
					{
						await channel.SendMessageAsync($"<@{e.Message.Author.Id}>'s post:", embed: embed as Embed);
						response = "All done!";
					}
					else if( embed is string )
					{
						response = $"{embed as string}\n{response}";
					}
				}

				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("recruit"));

			return commands;
		}

		private object GetRecruitmentEmbed(CommandArguments e)
		{
			Dictionary<PropertySpecification, string> fields = new Dictionary<PropertySpecification, string>();

			MatchCollection matches = this.CommandParamRegex.Matches(e.TrimmedMessage);
			foreach( Match match in matches )
			{
				string optionString = this.CommandOptionRegex.Match(match.Value).Value;
				string value = match.Value.Substring(optionString.Length + 1).Replace('`', '\'');

				PropertySpecification property = this.Properties.FirstOrDefault(p => p.Options.Contains(optionString));
				if( property == null || (property.ValidValues != null && property.ValidValues.Length > 4 && property.ValidValues.All(v => v.ToLower() != value.ToLower())) || value.Length > property.CharacterLimit )
					return $"`{value}` is invalid..";

				if( property.ValidValues != null)
				{
					MatchCollection parsedValues = this.CommandValueRegex.Matches(value);
					if( property.ValidValues.Length <= 4 )
					{
						string newValue = "";
						for( int i = 0; i < parsedValues.Count; i++ )
						{
							string val = property.ValidValues.FirstOrDefault(v => v.ToLower() == parsedValues[i].Value.ToLower());
							if( string.IsNullOrEmpty(val) )
								return $"{value} is invalid...";

							newValue += i == 0 ? $"`{val}`" : $" | `{val}`";
						}
						value = newValue;
					}
					else
					{
						string val = property.ValidValues.FirstOrDefault(v => v.ToLower() == parsedValues[0].Value.ToLower());
						value = $"`{val}`";
					}
				}

				fields.Add(property, value);
			}

			EmbedBuilder embedBuilder = new EmbedBuilder();
			embedBuilder.Author = new EmbedAuthorBuilder().WithName(fields.First(f => f.Key.Order == 0).Value);
			embedBuilder.Color = Color.Orange;

			if( fields.ContainsKey(this.Properties.First()) )
			{
				embedBuilder.ThumbnailUrl = embedBuilder.Author.IconUrl = fields.FirstOrDefault(f => f.Key.Order == -1).Value;
			}

			foreach( PropertySpecification property in this.Properties )
			{
				if( !fields.ContainsKey(property) && !property.Optional )
					return $"{property.Label} is missing";

				if( !fields.ContainsKey(property) || property.Order <= 0 )
					continue;

				embedBuilder.AddField(property.Label, $"{property.Prefix}{fields[property]}{property.Suffix}", property.Inline);
			}

			return embedBuilder.Build();
		}

		private void ModifyRecruitmentEmbed(CommandArguments e, SocketUserMessage message)
		{
			throw new NotImplementedException();
		}

		public Task Update(IBotwinderClient iClient)
		{
			return Task.CompletedTask;
		}
	}
}
