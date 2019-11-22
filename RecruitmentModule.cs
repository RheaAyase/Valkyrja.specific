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
			new PropertySpecification(1, true, false, "Current Composition", new string[]{"-c", "--composition"}, new string[]{"PLD", "GNB", "DRK", "WAR", "WHM", "AST", "SCH", "SAM", "MCH", "BLM", "DNC", "BRD", "SMN", "MNK", "RDM", "NIN", "DRG"}, 30, "|", "`", "`"),
			new PropertySpecification(2, true, false, "Looking for", new string[]{"-l", "--lookingfor"}, new string[]{"<:pld:476449887290916876>", "<:gnb:581102434571780107>", "<:drk:476449887597101056>", "<:war:476449887702220810>", "<:whm:476449887261687809>", "<:ast:476449887274401798>", "<:sch:476449887546769409>", "<:sam:476449887547031574>", "<:mch:476449887140052993>", "<:blm:476449887312150548>", "<:dnc:581102425327403008>", "<:brd:476449887311888385>", "<:smn:476449887672729610>", "<:mnk:476449887198773249>", "<:rdm:476449887190515716>", "<:nin:476449887496568832>", "<:drg:476449887337316353>", "<:healer:476449887391842304>", "<:tank:477224797315530752>", "<:dps:357417680799793162>", "<:caster:347159836225699851>", "<:ranged:347159816331853826>", "<:melee:347159777991852033>"}, 150),
			new PropertySpecification(3, false, false, "Goals", new string[]{"-g", "--goals"}, null, 100),
			new PropertySpecification(4, false, false, "CurrentProgress", new string[]{"-p", "--progress"}, null, 100),
			new PropertySpecification(5, true, false, "Contact", new string[]{"-k", "--contact"}, null, 50),
			new PropertySpecification(6, true, true, "Schedule", new string[]{"-s", "--schedule"}, null, 100),
			new PropertySpecification(7, false, false, "Description", new string[]{"-d", "--description"}, null, 350)
		};

		private guid FfxivId = 142476055482073089;
		private Dictionary<string, guid> FfxivChannelIds = new Dictionary<string, ulong>(){
			["staticAether"] = 647459984870735892,
			["staticPrimal"] = 647460088302272532,
			["staticCrystal"] = 647460192446840842,
			["staticChaos"] = 647460277339684883,
			["staticLight"] = 647460352589561876,
			["staticElemental"] = 647460426019373075,
			["staticGaia"] = 647460490036903967,
			["staticMana"] = 647460558177828874
		};
		private Dictionary<guid, guid> FfxivMessageIds = new Dictionary<ulong, ulong>(){
			[647459984870735892] = 647480500424015892, //aether
			[647460088302272532] = 647480532321697820, //primal
			[647460192446840842] = 647480562017370151, //crystal
			[647460277339684883] = 647480602697793540, //chaos
			[647460352589561876] = 647480648428421121, //light
			[647460426019373075] = 647480706792030228, //elemental
			[647460490036903967] = 647480763813724179, //gaia
			[647460558177828874] = 647480798043176962  //mana
		};
		string FfxivHelpString = "```md\nCreate or modify your recruitment post with the following properties:\n\n" +
		                       "[ -b ][ --logo        ] | Optional URL to your logo (optional, up to 128px)\n" +
		                       "[ -n ][ --name        ] | Name of your group (up to 30char)\n" +
		                       "[ -c ][ --composition ] | Current composition - space delimited list of roles \n" +
		                       "[ -l ][ --lookingfor  ] | Looking for - class or role emojis\n" +
		                       "[ -g ][ --goals       ] | Group goals. Savage? Ultimate?\n" +
		                       "[ -p ][ --progress    ] | Current progress of the static\n" +
		                       "[ -s ][ --schedule  ]   | Raid schedule, don't forget timezone (UTC?) (optional)\n" +
		                       "[ -k ][ --contact  ]    | User mention of a contact to reach out to\n" +
		                       "[ -d ][ --description ] | Up to 500 characters of group description\n" +
		                       "\nNote: --description can be text that supports [markdown](links)." +
		                       "\nExample: ?recruitment --name Nine Valkyries -c PLD AST DNC -l :melee: -g Savage & Ultimate -p Cleared it all. -k @Valkyrja#7811 -d Die in Battle and Go to Valhalla!" +
		                       "\n```";

		private readonly Regex CommandParamRegex = new Regex("--?\\w+\\s(?!--?\\w|$).*?(?=\\s--?\\w|$)", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
		private readonly Regex CommandOptionRegex = new Regex("--?\\w+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
		private readonly Regex CommandValueRegex = new Regex("<?[:@]?!?\\w+((?=:):\\d+>)?>?", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
		private readonly Regex UserIdRegex = new Regex("\\d+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

		private BotwinderClient Client;

		public Func<Exception, string, guid, Task> HandleException{ get; set; }
		public bool DoUpdate{ get; set; } = false;

		public List<Command> Init(IBotwinderClient iClient)
		{
			this.Client = iClient as BotwinderClient;
			List<Command> commands = new List<Command>();

// !removeRecruitment
			Command newCommand = new Command("removeRecruitment");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.IsHidden = true;
			newCommand.OnExecute += async e => {
				if( e.Server.Id != this.EdcId && e.Server.Id != this.FfxivId )
					return;

				string response = "";
				List<SocketTextChannel> channels = new List<SocketTextChannel>();
				guid lastMessage = 0;
				if( e.Server.Id == this.EdcId )
				{
					response = this.EdcHelpString;
					channels.Add(e.Server.Guild.GetTextChannel(this.EdcChannelId));
					lastMessage = this.EdcMessageId;
				}
				else if( e.Server.Id == this.FfxivId )
				{
					if( !this.FfxivChannelIds.ContainsKey(e.CommandId) )
					{
						return;
					}

					response = this.FfxivHelpString;
					channels = this.FfxivChannelIds.Select(c => e.Server.Guild.GetTextChannel(c.Value)).ToList();
				}
				else
				{
					return;
				}

				foreach( SocketTextChannel channel in channels )
				{
					lastMessage = this.FfxivMessageIds[channel.Id];
					await DeletePreviousMessage(channel, lastMessage, e.Message.Author.Id);
				}

				await e.SendReplySafe("Byeee!");
			};
			commands.Add(newCommand);

// !recruitment
			newCommand = new Command("recruitment");
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
					if( !this.FfxivChannelIds.ContainsKey(e.CommandId) )
					{
						return;
					}

					response = this.FfxivHelpString;
					channel = e.Server.Guild.GetTextChannel(this.FfxivChannelIds[e.CommandId]);
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

				await DeletePreviousMessage(channel, lastMessage, e.Message.Author.Id);

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
			newCommand = newCommand.CreateCopy("staticAether");
			commands.Add(newCommand);
			newCommand = newCommand.CreateCopy("staticPrimal");
			commands.Add(newCommand);
			newCommand = newCommand.CreateCopy("staticCrystal");
			commands.Add(newCommand);
			newCommand = newCommand.CreateCopy("staticLight");
			commands.Add(newCommand);
			newCommand = newCommand.CreateCopy("staticChaos");
			commands.Add(newCommand);
			newCommand = newCommand.CreateCopy("staticElemental");
			commands.Add(newCommand);
			newCommand = newCommand.CreateCopy("staticGaia");
			commands.Add(newCommand);
			newCommand = newCommand.CreateCopy("staticMana");
			commands.Add(newCommand);

			return commands;
		}

		private async Task DeletePreviousMessage(IMessageChannel channel, guid lastMessage, guid authorId)
		{
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

			IMessage message = messages.FirstOrDefault(m => guid.TryParse(this.UserIdRegex.Match(m.Content).Value, out guid id) && id == authorId);
			if( message != null )
			{
				//todo if( message.embed == newembed ) return; ~ do not just bump the post without changing anything.
				//todo support modifying a single property...
				//await (message as SocketUserMessage).ModifyAsync(m => ModifyRecruitmentEmbed(e, m));
				//await e.SendReplySafe("All done.");
				//return;
				await message.DeleteAsync();
			}
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
			embedBuilder.Color = e.Server.Id == this.EdcId ? Color.Orange : Color.Purple;

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

				embedBuilder.AddField(property.Label, property.ValidValues == null ? $"{property.Prefix}{fields[property]}{property.Suffix}" : fields[property], property.Inline);
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
