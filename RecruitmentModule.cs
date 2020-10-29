using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Valkyrja.core;
using Valkyrja.entities;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using guid = System.UInt64;

namespace Valkyrja.modules
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
			public string RegexHelp = "";
			public Regex Regex = null;

			public PropertySpecification(int order, bool inline, bool optional, string label, string[] options, string[] validValues = null, int characterLimit=10, string delimiter = "", string prefix = "", string suffix = "", string regexHelp = "", string regex = null)
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
				if( !string.IsNullOrEmpty(regex) )
				{
					this.RegexHelp = regexHelp;
					this.Regex = new Regex(regex, RegexOptions.Compiled);
				}
			}
		}

		private class ServerConfiguration
		{
			private readonly Regex CommandParamRegex = new Regex("--?\\w+\\s(?!--?\\w|$).*?(?=\\s--?\\w|$)", RegexOptions.Compiled | RegexOptions.Singleline, TimeSpan.FromMilliseconds(100));
			private readonly Regex CommandOptionRegex = new Regex("--?\\w+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
			private readonly Regex CommandValueRegex = new Regex("<?[:@]?!?\\w+((?=:):\\d+>)?>?", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
			private readonly Regex UserIdRegex = new Regex("\\d+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

			private guid ServerId;
			private IEnumerable<PropertySpecification> Properties;
			private Dictionary<string, guid> CommandChannelPairs;
			private string HelpString;
			private Color Color;
			private int BumpDays;

			public ServerConfiguration(guid serverId, IEnumerable<PropertySpecification> properties, Dictionary<string, guid> commandChannelPairs, string helpString, Color color, int bumpDays)
			{
				this.ServerId = serverId;
				this.Properties = properties;
				this.CommandChannelPairs = commandChannelPairs;
				this.HelpString = helpString;
				this.Color = color;
				this.BumpDays = bumpDays;
			}

			public ServerConfiguration(guid serverId, IEnumerable<PropertySpecification> properties, Dictionary<string, guid> commandChannelPairs, string helpString, Color color, int bumpDays, Regex paramRegex, Regex optionRegex, Regex valueRegex)
			{
				this.ServerId = serverId;
				this.Properties = properties;
				this.CommandChannelPairs = commandChannelPairs;
				this.HelpString = helpString;
				this.Color = color;
				this.BumpDays = bumpDays;
				this.CommandParamRegex = paramRegex;
				this.CommandOptionRegex = optionRegex;
				this.CommandValueRegex = valueRegex;
			}

			public async Task Find(CommandArguments commandArgs)
			{
				foreach( guid channelId in this.CommandChannelPairs.Values )
				{
					SocketTextChannel channel = commandArgs.Server.Guild.GetTextChannel(channelId);
					if( channel == null )
						continue;

					await Find(channel, commandArgs);
				}
			}

			private async Task Find(IMessageChannel channel, CommandArguments commandArgs)
			{
				string response = "meep";
				bool found = false;
				int count = 0;
				guid lastMessageId = 0;
				while(true)
				{
					IEnumerable<IMessage> batch = await channel.GetMessagesAsync(lastMessageId+1, Direction.After, 100, CacheMode.AllowDownload).FlattenAsync();
					if( batch == null || !batch.Any() )
						break;
					lastMessageId = batch.Last().Id;

					count++;
					if( batch.FirstOrDefault(m => m?.Content != null && guid.TryParse(this.UserIdRegex.Match(m.Content).Value, out guid id) && guid.TryParse(commandArgs.TrimmedMessage, out guid argId) && id == argId) is IUserMessage message )
					{
						found = true;
						response = $"Found message `{message.Id}` at position `{count}`\n{message.Content}";
						break;
					}
				}

				if( !found )
					response = $"Not found after {count} messages.";

				await commandArgs.SendReplySafe(response);
			}

			public async Task DeletePreviousMessages(CommandArguments commandArgs)
			{
				foreach( guid channelId in this.CommandChannelPairs.Values )
				{
					SocketTextChannel channel = commandArgs.Server.Guild.GetTextChannel(channelId);
					if( channel == null )
						continue;

					await DeletePreviousMessages(channel, commandArgs.Message.Author.Id);
				}
			}

			private async Task DeletePreviousMessages(IMessageChannel channel, guid authorId)
			{
				guid lastMessageId = 0;
				while(true)
				{
					IEnumerable<IMessage> batch = await channel.GetMessagesAsync(lastMessageId+1, Direction.After, 100, CacheMode.AllowDownload).FlattenAsync();
					if( batch == null || !batch.Any() )
						break;
					lastMessageId = batch.Last().Id;

					IMessage message = batch.FirstOrDefault(m => m.Content != null && guid.TryParse(this.UserIdRegex.Match(m.Content).Value, out guid id) && id == authorId);
					if( message != null )
						await message.DeleteAsync();
				}
			}

			public async Task Bump(CommandArguments commandArgs)
			{
				string response = "Unknown error.";
				foreach( guid channelId in this.CommandChannelPairs.Values )
				{
					SocketTextChannel channel = commandArgs.Server.Guild.GetTextChannel(channelId);
					if( channel == null )
						continue;

					response = await Bump(channel, commandArgs.Message.Author.Id);
				}

				await commandArgs.SendReplySafe(response);
			}
			private async Task<string> Bump(IMessageChannel channel, guid authorId)
			{
				guid lastMessageId = 0;
				while(true)
				{
					IEnumerable<IMessage> batch = await channel.GetMessagesAsync(lastMessageId+1, Direction.After, 100, CacheMode.AllowDownload).FlattenAsync();
					if( batch == null || !batch.Any() )
						break;
					lastMessageId = batch.Last().Id;

					IMessage message = batch.FirstOrDefault(m => m.Content != null && guid.TryParse(this.UserIdRegex.Match(m.Content).Value, out guid id) && id == authorId);
					if( message != null )
					{
						TimeSpan diff = DateTime.UtcNow - Utils.GetTimeFromId(message.Id);
						if( diff < TimeSpan.FromDays(this.BumpDays) )
						{
							return $"Gotta wait {Utils.GetDurationString(TimeSpan.FromDays(this.BumpDays) - diff).Replace("for", "another")}";
						}

						await channel.SendMessageAsync(message.Content, embed: message.Embeds.First() as Embed);
						await message.DeleteAsync();
						return "Bump'd!";
					}
				}

				return "Nothing to bump into...";
			}

			public async Task SendOrReplaceEmbed(CommandArguments commandArgs)
			{
				if( !this.CommandChannelPairs.ContainsKey(commandArgs.CommandId.ToLower()) )
					return;

				if( string.IsNullOrEmpty(commandArgs.TrimmedMessage) || commandArgs.TrimmedMessage == "--help" )
				{
					await commandArgs.SendReplySafe(this.HelpString);
					return;
				}

				IMessageChannel channel = commandArgs.Server.Guild.GetTextChannel(this.CommandChannelPairs[commandArgs.CommandId.ToLower()]);

				object returnValue = GetRecruitmentEmbed(commandArgs.TrimmedMessage);

				if( returnValue != null )
				{
					string response = "Unknown error.";
					if( returnValue is Embed embed )
					{
						bool replaced = false;
						response = "All done!";
						guid lastMessageId = 0;
						while(true)
						{
							IEnumerable<IMessage> batch = await channel.GetMessagesAsync(lastMessageId+1, Direction.After, 100, CacheMode.AllowDownload).FlattenAsync();
							if( batch == null || !batch.Any() )
								break;
							lastMessageId = batch.Last().Id;

							IMessage message = batch.FirstOrDefault(m => m?.Content != null && guid.TryParse(this.UserIdRegex.Match(m.Content).Value, out guid id) && id == commandArgs.Message.Author.Id);
							if( message != null )
							{
								replaced = true;
								response = "I've modified your previous post.";
								switch( message )
								{
									case SocketUserMessage msg:
										await msg.ModifyAsync(m => m.Embed = embed);
										break;
									case RestUserMessage msg:
										await msg.ModifyAsync(m => m.Embed = embed);
										break;
									default:
										response = "I wasn't able to modify the old post.";
										break;
								}
								break;
							}
						}
						if( !replaced )
							await channel.SendMessageAsync($"<@{commandArgs.Message.Author.Id}>'s post:", embed: embed);
					}
					else if( returnValue is string errorText )
					{
						response = $"{errorText}\n_(Use `{commandArgs.Server.Config.CommandPrefix}{commandArgs.CommandId}` without arguments for help.)_";
					}
					await commandArgs.SendReplySafe(response);
				}
			}

			private object GetRecruitmentEmbed(string trimmedMessage)
			{
				Dictionary<PropertySpecification, string> fields = new Dictionary<PropertySpecification, string>();

				MatchCollection matches = this.CommandParamRegex.Matches(trimmedMessage);
				foreach( Match match in matches )
				{
					string optionString = this.CommandOptionRegex.Match(match.Value).Value;
					string value = match.Value.Substring(optionString.Length + 1).Replace('`', '\'');

					PropertySpecification property = this.Properties.FirstOrDefault(p => p.Options.Contains(optionString));

					if( property == null )
						return $"Invalid option:\n```\n{optionString}\n```";

					if( value.Length > property.CharacterLimit )
						return $"`{property.Label}` exceeds the character limit of `{property.CharacterLimit}` characters with `{value.Length}`:\n```\n{value}\n```";

					if( property.ValidValues != null )
					{
						MatchCollection parsedValues = this.CommandValueRegex.Matches(value);
						StringBuilder valueBuilder = new StringBuilder();
						for( int i = 0; i < parsedValues.Count; i++ )
						{
							string val = property.ValidValues.FirstOrDefault(v => string.Equals(v, parsedValues[i].Value, StringComparison.CurrentCultureIgnoreCase));
							if( string.IsNullOrEmpty(val) )
								return $"`{property.Label}` has invalid value:\n```\n{parsedValues[i]}\n```";

							val = $"{property.Prefix}{val}{property.Suffix}";
							valueBuilder.Append(i == 0 ? val : (property.Delimiter + val));
						}

						value = valueBuilder.ToString();
					}
					else if( property.Regex != null && !property.Regex.Match(value).Success )
					{
						string regexHelp = "";
						if( !string.IsNullOrEmpty(property.RegexHelp) )
							regexHelp = $"`{property.RegexHelp}`";
						return $"`{property.Label}` does not match prescribed format {regexHelp}\n```\n{value}\n```";
					}

					fields.Add(property, value);
				}

				PropertySpecification nameProperty = this.Properties.First(f => f.Order == 0);
				if( !fields.ContainsKey(nameProperty) )
					return $"`{nameProperty.Label}` is missing";

				EmbedBuilder embedBuilder = new EmbedBuilder();
				embedBuilder.Author = new EmbedAuthorBuilder().WithName(fields[nameProperty]);
				embedBuilder.Color = this.Color;

				if( fields.ContainsKey(this.Properties.First()) )
				{
					embedBuilder.ThumbnailUrl = embedBuilder.Author.IconUrl = fields.FirstOrDefault(f => f.Key.Order == -1).Value;
				}

				foreach( PropertySpecification property in this.Properties )
				{
					if( !fields.ContainsKey(property) && !property.Optional )
						return $"`{property.Label}` is missing";

					if( !fields.ContainsKey(property) || property.Order <= 0 )
						continue;

					embedBuilder.AddField(property.Label, property.ValidValues == null ? $"{property.Prefix}{fields[property]}{property.Suffix}" : fields[property], property.Inline);
				}

				return embedBuilder.Build();
			}
		}


		//This is a hardcoded server specific feature, the code is one huge hack and nobody should even read it. It's disgusting!
		//Especially the way I treat this list...
		private readonly Dictionary<guid, ServerConfiguration> ServerConfigurations = new Dictionary<guid, ServerConfiguration>(){
			[89778537522802688] = new ServerConfiguration(
				89778537522802688, // EDC
				new List<PropertySpecification>{
					new PropertySpecification(-1, false, true, "", new string[]{"-b", "--logo"}, null, 500),
					new PropertySpecification(0, false, false, "Name", new string[]{"-n", "--name"}, null, 30),
					new PropertySpecification(1, true, false, "Platforms", new string[]{"-p", "--platform"}, new string[]{"PC", "XBox", "PS4"}, 13, " | ", "`", "`"),
					new PropertySpecification(2, true, false, "Timezones", new string[]{"-t", "--timezone"}, new string[]{"EU", "NA", "APAC"}, 14, " | ", "`", "`"),
					new PropertySpecification(3, false, false, "Comms type", new string[]{"-c", "--comms"}, new string[]{"Discord", "TeamSpeak", "Mumble", "Ventrilo", "Steam"}),
					new PropertySpecification(4, true, true, "Squadron", new string[]{"-s", "--squadron"}, null, 30, "", "`", "`"),
					new PropertySpecification(5, true, true, "Squadron ID", new string[]{"-i", "--squadronId"}, null, 4, "", "`[", "]`"),
					new PropertySpecification(6, false, true, "Links", new string[]{"-l", "--link"}, null, 100),
					new PropertySpecification(7, false, false, "Description", new string[]{"-d", "--description"}, null, 500)
				},
				new Dictionary<string, guid>(){["recruitment"] = 754432646024790027, ["lfg"] = 754432646024790027},
				"```md\nCreate or modify your #recruitment post with the following properties:\n\n" +
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
				"\nExample: !lfg --name The Elite -p PC PS4 XBox -t EU NA APAC -c Discord --link [Discord](https://discord.gg/elite) -d Weirdoes." +
				"\n```",
				Color.Orange,
				14
			),
			[142476055482073089] = new ServerConfiguration(
				142476055482073089, // FFXIV
				new List<PropertySpecification>{
					new PropertySpecification(-1, false, true, "", new string[]{"-t", "--logo"}, null, 300),
					new PropertySpecification(0, false, false, "Name", new string[]{"-n", "--name"}, null, 30),
					new PropertySpecification(1, true, false, "Current Composition", new string[]{"-c", "--composition"}, new string[]{"PLD", "GNB", "DRK", "WAR", "WHM", "AST", "SCH", "SAM", "MCH", "BLM", "DNC", "BRD", "SMN", "MNK", "RDM", "NIN", "DRG"}, 30, "|", "`", "`"),
					new PropertySpecification(2, true, false, "Looking for", new string[]{"-l", "--lookingfor"}, new string[]{"<:pld:476449887290916876>", "<:gnb:581102434571780107>", "<:drk:476449887597101056>", "<:war:476449887702220810>", "<:whm:476449887261687809>", "<:ast:476449887274401798>", "<:sch:476449887546769409>", "<:sam:476449887547031574>", "<:mch:476449887140052993>", "<:blm:476449887312150548>", "<:dnc:581102425327403008>", "<:brd:476449887311888385>", "<:smn:476449887672729610>", "<:mnk:476449887198773249>", "<:rdm:476449887190515716>", "<:nin:476449887496568832>", "<:drg:476449887337316353>", "<:healer:476449887391842304>", "<:tank:477224797315530752>", "<:dps:357417680799793162>", "<:caster:347159836225699851>", "<:ranged:347159816331853826>", "<:melee:347159777991852033>", "<:blu:581102417257693207>"}, 150),
					new PropertySpecification(3, false, false, "Goals", new string[]{"-g", "--goals"}, null, 100),
					new PropertySpecification(4, false, false, "CurrentProgress", new string[]{"-p", "--progress"}, null, 100),
					new PropertySpecification(5, true, false, "Contact", new string[]{"-k", "--contact"}, null, 55, "", "", "", "@UserMention", "^<@!?\\d+>"),
					new PropertySpecification(6, true, true, "Schedule", new string[]{"-s", "--schedule"}, null, 150),
					new PropertySpecification(7, false, false, "Description", new string[]{"-d", "--description"}, null, 500)
				},
				new Dictionary<string, guid>(){
					["staticaether"] = 647459984870735892,
					["staticprimal"] = 647460088302272532,
					["staticcrystal"] = 647460192446840842,
					["staticchaos"] = 647460277339684883,
					["staticlight"] = 647460352589561876,
					["staticelemental"] = 647460426019373075,
					["staticgaia"] = 647460490036903967,
					["staticmana"] = 647460558177828874
				},
				"```md\nCreate or modify your recruitment post with the following properties. Remember, only the \"optional\" ones are optional.\n\n" +
				"[ -t ][ --logo        ] | URL to your logo (optional, up to 128px)\n" +
				"[ -n ][ --name        ] | Name of your group (up to 30char)\n" +
				"[ -c ][ --composition ] | Current composition - space delimited list of roles \n" +
				"[ -l ][ --lookingfor  ] | Looking for - class or role emojis\n" +
				"[ -g ][ --goals       ] | Group goals. Savage? Ultimate?\n" +
				"[ -p ][ --progress    ] | Current progress of the static\n" +
				"[ -s ][ --schedule    ] | Raid schedule, don't forget timezone (UTC?) (optional)\n" +
				"[ -k ][ --contact     ] | User mention of a contact person to reach out to\n" +
				"[ -d ][ --description ] | Up to 500 characters of group description\n" +
				"\nNote: --description can be text that supports [markdown](links)." +
				"\nExample: ?lfg --name Nine Valkyries -c PLD AST DNC -l :melee: -g Savage & Ultimate -p Cleared it all. -k @Valkyrja#7811 -d Die in Battle and Go to Valhalla!" +
				"\n```",
				Color.Purple,
				14
			)
			//[] = new ServerConfiguration(),
		};


		private ValkyrjaClient Client;

		public Func<Exception, string, guid, Task> HandleException{ get; set; }
		public bool DoUpdate{ get; set; } = false;

		public List<Command> Init(IValkyrjaClient iClient)
		{
			this.Client = iClient as ValkyrjaClient;
			List<Command> commands = new List<Command>();

// !lfgBump
			Command newCommand = new Command("lfgBump");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.IsHidden = true;
			newCommand.OnExecute += async e => {
				if( !this.ServerConfigurations.ContainsKey(e.Server.Id) )
					return;

				await this.ServerConfigurations[e.Server.Id].Bump(e);
			};
			commands.Add(newCommand);

// !lfgRemove
			newCommand = new Command("lfgRemove");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.IsHidden = true;
			newCommand.OnExecute += async e => {
				if( !this.ServerConfigurations.ContainsKey(e.Server.Id) )
					return;

				await this.ServerConfigurations[e.Server.Id].DeletePreviousMessages(e);

				await e.SendReplySafe("Byeee!");
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("removeRecruitment"));

// !lfgFind
			newCommand = new Command("lfgFind");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "";
			newCommand.RequiredPermissions = PermissionType.OwnerOnly;
			newCommand.IsHidden = true;
			newCommand.OnExecute += async e => {
				if( !this.ServerConfigurations.ContainsKey(e.Server.Id) )
					return;

				await this.ServerConfigurations[e.Server.Id].Find(e);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("removeRecruitment"));

// !lfg
			newCommand = new Command("lfg");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.IsHidden = true;
			newCommand.OnExecute += async e => {
				if( !this.ServerConfigurations.ContainsKey(e.Server.Id) )
					return;

				await this.ServerConfigurations[e.Server.Id].SendOrReplaceEmbed(e);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("recruitment"));
			commands.Add(newCommand.CreateCopy("staticAether"));
			commands.Add(newCommand.CreateCopy("staticPrimal"));
			commands.Add(newCommand.CreateCopy("staticCrystal"));
			commands.Add(newCommand.CreateCopy("staticLight"));
			commands.Add(newCommand.CreateCopy("staticChaos"));
			commands.Add(newCommand.CreateCopy("staticElemental"));
			commands.Add(newCommand.CreateCopy("staticGaia"));
			commands.Add(newCommand.CreateCopy("staticMana"));

			return commands;
		}

		public Task Update(IValkyrjaClient iClient)
		{
			return Task.CompletedTask;
		}
	}
}
