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

			public PropertySpecification(int order, bool inline, bool optional, string label, string[] options, string[] validValues = null, int characterLimit=10)
			{
				this.Order = order;
				this.Inline = inline;
				this.Optional = optional;
				this.Label = label;
				this.Options = options;
				this.ValidValues = validValues;
				this.CharacterLimit = characterLimit;
			}
		}

		//This is a hardcoded server specific feature, the code is one huge hack and nobody should even read it. It's disgusting!
		//Especially the way I treat this list...
		private readonly IEnumerable<PropertySpecification> Properties = new List<PropertySpecification>{
			new PropertySpecification(-1, false, true, "", new string[]{"-b", "--logo"}, null, 500),
			new PropertySpecification(0, false, false, "Name", new string[]{"-n", "--name"}, null, 30),
			new PropertySpecification(1, true, false, "Platforms", new string[]{"-p", "--platform"}, new string[]{"PC", "XBox", "PS4"}),
			new PropertySpecification(2, true, false, "Timezones", new string[]{"-t", "--timezone"}, new string[]{"EU", "NA", "APAC"}),
			new PropertySpecification(3, false, false, "Comms type", new string[]{"-c", "--comms"}, new string[]{"Discord", "TeamSpeak", "Mumble", "Ventrilo", "Steam"}),
			new PropertySpecification(4, true, true, "Squadron", new string[]{"-s", "--squadron"}, null, 30),
			new PropertySpecification(5, true, true, "Squadron ID", new string[]{"-i", "--squadronId"}, null, 4),
			new PropertySpecification(6, false, true, "Links", new string[]{"-l", "--link"}, null, 100),
			new PropertySpecification(7, false, false, "Description", new string[]{"-d", "--description"}, null, 300)
		};

		private readonly Regex ProfileParamRegex = new Regex("--?\\w+\\s(?!--?\\w|$).*?(?=\\s--?\\w|$)", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
		private readonly Regex ProfileOptionRegex = new Regex("--?\\w+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
		private readonly Regex ProfileValueRegex = new Regex("\\w+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

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

				string response = "```md\n Create or modify your #recruitment post with the following properties:\n\n" +
				                  "[ -b ][ --logo        ] | Optional URL to your logo (up to 128px)\n" +
				                  "[ -n ][ --name        ] | Name of your playergroup (up to 30char)\n" +
				                  "[ -p ][ --platform    ] | Platform: PC, XBox or PS4\n" +
				                  "[ -t ][ --timezone    ] | Timezone: EU, NA or APAC\n" +
				                  "[ -c ][ --comms       ] | Comms type: Discord, TeamSpeak, Mumble, Ventrilo or Steam\n" +
				                  "[ -s ][ --squadron    ] | Name of the squadron (optional field up to 30char)\n" +
				                  "[ -i ][ --squadronId  ] | [ID] of the squadron (optional field)\n" +
				                  "[ -l ][ --link        ] | Links to your Inara or Discord (optional field up to 100char)\n" +
				                  "[ -d ][ --description ] | Up to 300 characters of group description.\n" +
				                  "\nNote: --comms, --link and --description can be text that supports [markdown](links)." +
				                  "\nExample: !recruitment --name The Elite -p PC PS4 XBox -t EU NA APAC -c [Discord](https://discord.gg/elite) -d Weirdoes." +
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
					lastMessage = messages.First().Id; //Assuming that the first message is the most recent.
					messages.AddRange(downloaded);
				} while( downloadedCount >= 100 );

				IMessage message = messages.FirstOrDefault(m => m.Author.Id == e.Message.Author.Id);
				if( message != null )
				{
					//todo support modifying a single property...
					//await (message as SocketUserMessage).ModifyAsync(m => ModifyRecruitmentEmbed(e, m));
					//await e.SendReplySafe("All done.");
					//return;
					await message.DeleteAsync();
				}

				Embed embed = GetRecruitmentEmbed(e);
				if( embed != null )
				{
					await channel.SendMessageAsync("", embed: embed);
					response = "All done!";
				}

				await e.SendReplySafe(response);
			};
			commands.Add(newCommand);
			commands.Add(newCommand.CreateAlias("recruit"));

			return commands;
		}

		private Embed GetRecruitmentEmbed(CommandArguments e)
		{
			Dictionary<PropertySpecification, string> fields = new Dictionary<PropertySpecification, string>();

			MatchCollection matches = this.ProfileParamRegex.Matches(e.TrimmedMessage);
			foreach( Match match in matches )
			{
				string optionString = this.ProfileOptionRegex.Match(match.Value).Value;
				string value = match.Value.Substring(optionString.Length + 1).Replace('`', '\'');

				PropertySpecification property = this.Properties.FirstOrDefault(p => p.Options.Contains(optionString));
				if( property == null || (property.ValidValues != null && property.ValidValues.Length > 4 && property.ValidValues.Contains(value.ToLower())) || value.Length > property.CharacterLimit )
					return null;

				if( property.ValidValues != null)
				{
					MatchCollection parsedValues = this.ProfileValueRegex.Matches(value);
					if( property.ValidValues.Length <= 4 )
					{
						value = "";
						for( int i = 0; i < parsedValues.Count; i++ )
						{
							string val = property.ValidValues.FirstOrDefault(v => v.Contains(parsedValues[i].Value.ToLower()));
							if( string.IsNullOrEmpty(val) )
								return null;

							value += i == 0 ? $"`{val}`" : $" | `{val}`";
						}
					}
					else
					{
						string val = property.ValidValues.FirstOrDefault(v => v.Contains(parsedValues[0].Value.ToLower()));
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
				embedBuilder.ThumbnailUrl = embedBuilder.Author.Url = fields.FirstOrDefault(f => f.Key.Order == -1).Value;
			}

			foreach( PropertySpecification property in this.Properties )
			{
				if( !fields.ContainsKey(property) && !property.Optional )
					return null;

				if( !fields.ContainsKey(property) || property.Order <= 0 )
					continue;

				embedBuilder.AddField(property.Label, fields.Values, property.Inline);
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
