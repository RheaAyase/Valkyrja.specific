﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Valkyrja.core;
using Valkyrja.entities;
using Discord;
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
			new PropertySpecification(2, true, false, "Looking for", new string[]{"-l", "--lookingfor"}, new string[]{"<:pld:476449887290916876>", "<:gnb:581102434571780107>", "<:drk:476449887597101056>", "<:war:476449887702220810>", "<:whm:476449887261687809>", "<:ast:476449887274401798>", "<:sch:476449887546769409>", "<:sam:476449887547031574>", "<:mch:476449887140052993>", "<:blm:476449887312150548>", "<:dnc:581102425327403008>", "<:brd:476449887311888385>", "<:smn:476449887672729610>", "<:mnk:476449887198773249>", "<:rdm:476449887190515716>", "<:nin:476449887496568832>", "<:drg:476449887337316353>", "<:healer:476449887391842304>", "<:tank:477224797315530752>", "<:dps:357417680799793162>", "<:caster:347159836225699851>", "<:ranged:347159816331853826>", "<:melee:347159777991852033>", "<:blu:581102417257693207>"}, 150),
			new PropertySpecification(3, false, false, "Goals", new string[]{"-g", "--goals"}, null, 100),
			new PropertySpecification(4, false, false, "CurrentProgress", new string[]{"-p", "--progress"}, null, 100),
			new PropertySpecification(5, true, false, "Contact", new string[]{"-k", "--contact"}, null, 55, "", "", "", "@UserMention", "^<@!?\\d+>"),
			new PropertySpecification(6, true, true, "Schedule", new string[]{"-s", "--schedule"}, null, 150),
			new PropertySpecification(7, false, false, "Description", new string[]{"-d", "--description"}, null, 500)
		};

		private guid LightId = 424542406860603434;
		private guid LightChannelId = 142476055482073089;
		private guid FfxivId = 142476055482073089;
		private Dictionary<string, guid> FfxivChannelIds = new Dictionary<string, ulong>(){
			["staticaether"] = 647459984870735892,
			["staticprimal"] = 647460088302272532,
			["staticcrystal"] = 647460192446840842,
			["staticchaos"] = 647460277339684883,
			["staticlight"] = 647460352589561876,
			["staticelemental"] = 647460426019373075,
			["staticgaia"] = 647460490036903967,
			["staticmana"] = 647460558177828874
		};
		string FfxivHelpString = "```md\nCreate or modify your recruitment post with the following properties. Remember, only the \"optional\" ones are optional.\n\n" +
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
		                       "\nExample: ?recruitment --name Nine Valkyries -c PLD AST DNC -l :melee: -g Savage & Ultimate -p Cleared it all. -k @Valkyrja#7811 -d Die in Battle and Go to Valhalla!" +
		                       "\n```";

		private readonly Regex CommandParamRegex = new Regex("--?\\w+\\s(?!--?\\w|$).*?(?=\\s--?\\w|$)", RegexOptions.Compiled | RegexOptions.Singleline, TimeSpan.FromMilliseconds(100));
		private readonly Regex CommandOptionRegex = new Regex("--?\\w+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
		private readonly Regex CommandValueRegex = new Regex("<?[:@]?!?\\w+((?=:):\\d+>)?>?", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
		private readonly Regex UserIdRegex = new Regex("\\d+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

		private ValkyrjaClient Client;

		public Func<Exception, string, guid, Task> HandleException{ get; set; }
		public bool DoUpdate{ get; set; } = false;

		public List<Command> Init(IValkyrjaClient iClient)
		{
			this.Client = iClient as ValkyrjaClient;
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

				List<SocketTextChannel> channels = new List<SocketTextChannel>();
				if( e.Server.Id == this.EdcId )
				{
					channels.Add(e.Server.Guild.GetTextChannel(this.EdcChannelId));
				}
				else if( e.Server.Id == this.LightId )
				{
					channels.Add(e.Server.Guild.GetTextChannel(this.LightChannelId));
				}
				else if( e.Server.Id == this.FfxivId )
				{
					channels = this.FfxivChannelIds.Select(c => e.Server.Guild.GetTextChannel(c.Value)).ToList();
				}
				else
				{
					return;
				}

				foreach( SocketTextChannel channel in channels )
				{
					await DeletePreviousMessage(channel, e.Message.Author.Id);
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
				if( e.Server.Id == this.EdcId )
				{
					response = this.EdcHelpString;
					channel = e.Server.Guild.GetTextChannel(this.EdcChannelId);
				}
				else if( e.Server.Id == this.LightId )
				{
					response = this.FfxivHelpString;
					channel = e.Server.Guild.GetTextChannel(this.LightChannelId);
				}
				else if( e.Server.Id == this.FfxivId )
				{
					if( !this.FfxivChannelIds.ContainsKey(e.CommandId.ToLower()) )
					{
						return;
					}

					response = this.FfxivHelpString;
					channel = e.Server.Guild.GetTextChannel(this.FfxivChannelIds[e.CommandId.ToLower()]);
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

				await DeletePreviousMessage(channel, e.Message.Author.Id);

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
						response = $"{embed as string}\n_(Use `{e.Server.Config.CommandPrefix}{e.CommandId}` without arguments for help.)_";
					}
				}

				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("recruit"));
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

		private async Task DeletePreviousMessage(IMessageChannel channel, guid authorId)
		{
			await foreach( IReadOnlyCollection<IMessage> list in channel.GetMessagesAsync(0, Direction.After, 1000, CacheMode.AllowDownload) )
			{
				IMessage message = list.FirstOrDefault(m => guid.TryParse(this.UserIdRegex.Match(m.Content).Value, out guid id) && id == authorId);
				if( message != null )
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
					return $"`{property.Label}` exceeds the character limit of `{property.CharacterLimit}` characters with `{value.Length}`:\n```\n{value}\n```";

				if( property.ValidValues != null)
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

			PropertySpecification nameProperty = properties.First(f => f.Order == 0);
			if( !fields.ContainsKey(nameProperty) )
				return $"`{nameProperty.Label}` is missing";

			EmbedBuilder embedBuilder = new EmbedBuilder();
			embedBuilder.Author = new EmbedAuthorBuilder().WithName(fields[nameProperty]);
			embedBuilder.Color = e.Server.Id == this.EdcId ? Color.Orange : Color.Purple;

			if( fields.ContainsKey(properties.First()) )
			{
				embedBuilder.ThumbnailUrl = embedBuilder.Author.IconUrl = fields.FirstOrDefault(f => f.Key.Order == -1).Value;
			}

			foreach( PropertySpecification property in properties )
			{
				if( !fields.ContainsKey(property) && !property.Optional )
					return $"`{property.Label}` is missing";

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

		public Task Update(IValkyrjaClient iClient)
		{
			return Task.CompletedTask;
		}
	}
}
