using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Botwinder.core;
using Botwinder.entities;
using Discord;
using Discord.WebSocket;
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
			public string Delimiter = "";
			public string Prefix = "";
			public string Suffix = "";

			public PropertySpecification(int order, bool inline, bool optional, string label, string[] options, string[] validValues = null, int characterLimit=10, string delimiter = "", string prefix = "", string suffix = "")
			{
				this.Order = order;
				this.Inline = inline;
				this.Optional = optional;
				this.Label = label;
				this.Options = options;
				this.ValidValues = validValues;
				this.CharacterLimit = characterLimit;
				this.Delimiter = delimiter;
				this.Prefix = prefix;
				this.Suffix = suffix;
			}
		}

		//This is a hardcoded server specific feature, the code is one huge hack and nobody should even read it. It's disgusting!
		//Especially the way I treat this list...
		private readonly IEnumerable<PropertySpecification> EdcProperties = new List<PropertySpecification>{
			new PropertySpecification(-1, false, true, "", new string[]{"-b", "--logo"}, null, 500),
			new PropertySpecification(0, false, false, "Name", new string[]{"-n", "--name"}, null, 30),
			new PropertySpecification(1, true, false, "Platforms", new string[]{"-p", "--platform"}, new string[]{"PC", "XBox", "PS4"}, 13, " | ", "`", "`"),
			new PropertySpecification(2, true, false, "Timezones", new string[]{"-t", "--timezone"}, new string[]{"EU", "NA", "APAC"}, 14, " | ", "`", "`"),
			new PropertySpecification(3, false, false, "Comms type", new string[]{"-c", "--comms"}, new string[]{"Discord", "TeamSpeak", "Mumble", "Ventrilo", "Steam"}),
			new PropertySpecification(4, true, true, "Squadron", new string[]{"-s", "--squadron"}, null, 30, "", "`", "`"),
			new PropertySpecification(5, true, true, "Squadron ID", new string[]{"-i", "--squadronId"}, null, 4, "", "`[", "]`"),
			new PropertySpecification(6, false, true, "Links", new string[]{"-l", "--link"}, null, 100),
			new PropertySpecification(7, false, false, "Description", new string[]{"-d", "--description"}, null, 500)
		};

		private guid EdcId = 89778537522802688;
		private guid EdcChannelId = 523925543045955594;
		private guid EdcMessageId = 523927539983581196;
		string EdcHelpString = "```md\nCreate or modify your #recruitment post with the following properties:\n\n" +
		                  "[ -t ][ --logo        ] | Optional URL to your logo (up to 128px)\n" +
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

		private readonly IEnumerable<PropertySpecification> FfxivProperties = new List<PropertySpecification>{
			new PropertySpecification(-1, false, true, "", new string[]{"-t", "--logo"}, null, 300),
			new PropertySpecification(0, false, false, "Name", new string[]{"-n", "--name"}, null, 30),
			new PropertySpecification(1, true, false, "Current Composition", new string[]{"-c", "--composition"}, new string[]{"PLD", "GNB", "DRK", "WAR", "WHM", "AST", "SCH", "SAM", "MCH", "BLM", "DNC", "BRD", "SMN", "MNK", "RDM", "NIN", "DRG"}, 30, " ", "`", "`"),
			new PropertySpecification(2, true, false, "Looking for", new string[]{"-l", "--lookingfor"}, new string[]{":pld:", ":gnb:", ":drk:", ":war:", ":whm:", ":ast:", ":sch:", ":sam:", ":mch:", ":blm:", ":dnc:", ":brd:", ":smn:", ":mnk:", ":rdm:", ":nin:", ":drg:", ":healer:", ":tank:", ":dps:", ":caster:", ":ranged:", ":melee:"}, 40, ""),
			new PropertySpecification(3, false, false, "Goals", new string[]{"-g", "--goals"}, null, 100),
			new PropertySpecification(4, false, false, "CurrentProgress", new string[]{"-p", "--progress"}, null, 100),
			new PropertySpecification(5, false, true, "Schedule", new string[]{"-s", "--schedule"}, null, 100),
			new PropertySpecification(5, false, false, "Contact", new string[]{"-c", "--contact"}, null, 100),
			new PropertySpecification(6, false, false, "Description", new string[]{"-d", "--description"}, null, 350)
		};

		private guid FfxivId = 142476055482073089;
		private Dictionary<guid, guid> FfxivChannelIds = new Dictionary<ulong, ulong>(){
			[638837450046963722] = 638837450046963722,
			[2] = 22
		};
		private Dictionary<guid, guid> FfxivMessageIds = new Dictionary<ulong, ulong>(){
			[638837450046963722] = 643810161747689502,
			[2] = 22
		};
		string FfxivHelpString = "```md\nCreate or modify your recruitment post with the following properties:\n\n" +
		                       "[ -b ][ --logo        ] | Optional URL to your logo (up to 128px)\n" +
		                       "[ -n ][ --name        ] | Name of your group (up to 30char)\n" +
		                       "[ -c ][ --composition ] | Current composition - space delimited list of roles \n" +
		                       "[ -l ][ --lookingfor    ] | Timezone: EU, NA or APAC\n" +
		                       "[ -g ][ --goals       ] | Comms type: Discord, TeamSpeak, Mumble, Ventrilo or Steam\n" +
		                       "[ -p ][ --progress    ] | Name of the squadron (optional field up to 30char)\n" +
		                       "[ -s ][ --schedule  ] | [ID] of the squadron (optional field = 4char)\n" +
		                       "[ -d ][ --description ] | Up to 500 characters of group description.\n" +
		                       "\nNote: --description can be text that supports [markdown](links)." +
		                       "\nExample: ?recruitment --name Nine Valkyries -c PLD AST DNC -l :melee: -g Savage & Alex -p Cleared it all. -c @Valkyrja#7811 -d Die in Battle and Go to Valhalla!" +
		                       "\n```";

		private readonly Regex CommandParamRegex = new Regex("--?\\w+\\s(?!--?\\w|$).*?(?=\\s--?\\w|$)", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
		private readonly Regex CommandOptionRegex = new Regex("--?\\w+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
		private readonly Regex CommandValueRegex = new Regex(":?\\w+:?", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
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
				if( e.Server.Id != this.EdcId && e.Server.Id != this.FfxivId )
					return;

				string response = "";
				IMessageChannel channel = null;
				guid lastMessage = 0;
				if( e.Server.Id == this.EdcId )
				{
					response = this.EdcHelpString;
					channel = e.Server.Guild.GetTextChannel(this.EdcChannelId);
					lastMessage = this.EdcMessageId;
				}
				else if( e.Server.Id == this.FfxivId )
				{
					if( !this.FfxivChannelIds.ContainsKey(e.Channel.Id) )
					{
						return;
					}

					response = this.FfxivHelpString;
					channel = e.Server.Guild.GetTextChannel(this.FfxivChannelIds[e.Channel.Id]);
					lastMessage = this.FfxivMessageIds[channel.Id];
				}
				else
				{
					return;
				}

				if( string.IsNullOrEmpty(e.TrimmedMessage) || e.TrimmedMessage == "--help" )
				{
					await e.SendReplySafe(response);
					return;
				}

				List<IMessage> messages = new List<IMessage>();
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
						response = $"{embed as string}\n\n{response}";
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
			IEnumerable<PropertySpecification> properties = e.Server.Id == this.EdcId ? this.EdcProperties : this.FfxivProperties;

			MatchCollection matches = this.CommandParamRegex.Matches(e.TrimmedMessage);
			foreach( Match match in matches )
			{
				string optionString = this.CommandOptionRegex.Match(match.Value).Value;
				string value = match.Value.Substring(optionString.Length + 1).Replace('`', '\'');

				PropertySpecification property = properties.FirstOrDefault(p => p.Options.Contains(optionString));

				if( property == null )
					return $"Invalid option:\n```\n{optionString}\n```";

				if( value.Length > property.CharacterLimit )
					return $"Value exceeds the character limit of `{property.CharacterLimit}` characters with `{value.Length}`:\n```\n{value}\n```";

				if( property.ValidValues != null)
				{
					MatchCollection parsedValues = this.CommandValueRegex.Matches(value);
						StringBuilder valueBuilder = new StringBuilder();
						for( int i = 0; i < parsedValues.Count; i++ )
						{
							string val = property.ValidValues.FirstOrDefault(v => string.Equals(v, parsedValues[i].Value, StringComparison.CurrentCultureIgnoreCase));
							if( string.IsNullOrEmpty(val) )
								return $"Invalid value:\n```\n{parsedValues[i]}\n```";

							val = $"{property.Prefix}{val}{property.Suffix}";
							valueBuilder.Append(i == 0 ? val : (property.Delimiter + val));
						}
						value = valueBuilder.ToString();
				}

				fields.Add(property, value);
			}

			EmbedBuilder embedBuilder = new EmbedBuilder();
			embedBuilder.Author = new EmbedAuthorBuilder().WithName(fields.First(f => f.Key.Order == 0).Value);
			embedBuilder.Color = Color.Orange;

			if( fields.ContainsKey(properties.First()) )
			{
				embedBuilder.ThumbnailUrl = embedBuilder.Author.IconUrl = fields.FirstOrDefault(f => f.Key.Order == -1).Value;
			}

			foreach( PropertySpecification property in properties )
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
