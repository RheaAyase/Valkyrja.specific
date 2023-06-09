using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Valkyrja.core;
using Valkyrja.entities;
using Discord;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using guid = System.UInt64;

namespace Valkyrja.modules
{
	public class MessageFilter : IModule
	{
		class FilterSpecification
		{
			public guid ServerId;
			public Regex Regex = null;
			public string ReplaceWith = null;

			public FilterSpecification(guid serverId, Regex regex, string replaceWith)
			{
				this.ServerId = serverId;
				this.Regex = regex;
				this.ReplaceWith = replaceWith;

			}
		}

		//This is a hardcoded server specific feature, the code is one huge hack and nobody should even read it. It's disgusting!
		//Especially the way I treat this list...
		private readonly Dictionary<guid, FilterSpecification> ServerConfigurations = new Dictionary<guid, FilterSpecification>(){
				[552293123766878208] = new FilterSpecification(
					552293123766878208, // Chill Homelab
					new Regex("https?://(www\\.)?reddit\\.com", RegexOptions.Compiled | RegexOptions.Singleline, TimeSpan.FromMilliseconds(100)),
					"https://old.reddit.com"
			)};

		private ValkyrjaClient Client;

		public Func<Exception, string, guid, Task> HandleException{ get; set; }
		public bool DoUpdate{ get; set; } = false;

		public List<Command> Init(IValkyrjaClient iClient)
		{
			this.Client = iClient as ValkyrjaClient;
			this.Client.Events.MessageReceived += OnMessageReceived;

			List<Command> commands = new List<Command>();

// !stuff
			/*Command newCommand = new Command("stuff");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "";
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.IsHidden = true;
			newCommand.OnExecute += async e => {
				//dostuff
			};
			commands.Add(newCommand);*/

			return commands;
		}

		private async Task OnMessageReceived(SocketMessage message)
		{
			FilterSpecification config = null;
			if( message.Channel is not SocketTextChannel channel || !this.ServerConfigurations.ContainsKey(channel.Guild.Id) || (config = this.ServerConfigurations[channel.Guild.Id]) == null )
				return;

			if( !config.Regex.IsMatch(message.Content) )
				return;
			
			try
			{
				string output = config.Regex.Replace(message.Content, config.ReplaceWith).Replace("@everyone", "@-everyone").Replace("@here", "@-here");
				await channel.SendMessageSafe($"**__{message.Author.Username} said:__**\n{output}");
				await message.DeleteAsync();

			} catch(Exception exception)
			{
				await this.Client.LogException(exception, "Custom message filter failed in # {channel.Id}", channel.Guild.Id);
			}
		}

		public Task Update(IValkyrjaClient iClient)
		{
			return Task.CompletedTask;
		}
	}
}
