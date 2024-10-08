using DSharpPlus.Entities;
using System.ComponentModel;
using System.Text;

namespace AssistantAI.Events;

// All the tools methods of the GuildEvent class.
public partial class GuildEvent {
    [Description("Get information about a user.")]

    async Task<string> GetUserInfo([Description("The user ID to get information about.")] ulong userID) {
        DiscordUser? user = await client.GetUserAsync(userID);
        if(user == null) //Ingore the 'CS8625: Cannot convert null literal to non-nullable reference type.' warning. I have no idea why it's there.
            return $"User with ID {userID} was not found.";

        string? customActivity = user.Presence?.Activities.FirstOrDefault(activity => activity.ActivityType == DiscordActivityType.Custom)?.RichPresence.State;

        var stringBuilder = new StringBuilder();
        stringBuilder.Append($"User: {user.GlobalName ?? user.Username}");
        stringBuilder.Append($" | Status: {Enum.GetName(user.Presence?.Status ?? DiscordUserStatus.Offline)}");
        if(customActivity != null)
            stringBuilder.Append($" | Custom Activity: {customActivity}");

        return $"[{stringBuilder}]";
    }

    //TODO: Make these tools methods with the new chat message system.

    //[Description("Add or overwrite a key in the user memory.")]
    //string AddOrOverwriteUserMemory(ToolTrigger toolTrigger, [Description("The key to add or overwrite.")] string key, [Description("The value to add or overwrite.")] string value) {
    //    if(!UserMemory.ContainsKey(toolTrigger.User!.Id))
    //        UserMemory[toolTrigger.User.Id] = [];

    //    UserMemory[toolTrigger.User.Id][key] = value;
    //    return $"User memory key {key} has been added or overwritten with value {value}";
    //}

    //[Description("Add or overwrite a key in the guild memory.")]
    //string AddOrOverwriteGuildMemory(ToolTrigger toolTrigger, [Description("The key to add or overwrite.")] string key, [Description("The value to add or overwrite.")] string value) {
    //    if(toolTrigger.Guild! == null!)
    //        throw new ArgumentException("You must be in a guild to use this command");
    //    else if(!GuildMemory.ContainsKey(toolTrigger.Guild.Id))
    //        GuildMemory[toolTrigger.Guild.Id] = [];

    //    GuildMemory[toolTrigger.Guild.Id][key] = value;
    //    return $"Guild memory key {key} has been added or overwritten with value {value}";
    //}

    //[Description("Remove a key from the user memory.")]
    //string RemoveUserMemory(ToolTrigger toolTrigger, [Description("The key to remove.")] string key) {
    //    if(!UserMemory.ContainsKey(toolTrigger.User!.Id))
    //        UserMemory[toolTrigger.User.Id] = [];

    //    if(UserMemory[toolTrigger.User.Id].Remove(key))
    //        return $"User memory key {key} has been removed";
    //    return $"User memory key {key} was not found";
    //}

    //[Description("Remove a key from the guild memory.")]
    //string RemoveGuildMemory(ToolTrigger toolTrigger, [Description("The key to remove.")] string key) {
    //    if(toolTrigger.Guild! == null!)
    //        throw new ArgumentException("You must be in a guild to use this command");
    //    else if(!GuildMemory.ContainsKey(toolTrigger.Guild.Id))
    //        GuildMemory[toolTrigger.Guild.Id] = [];

    //    if(GuildMemory[toolTrigger.Guild.Id].Remove(key))
    //        return $"Guild memory key {key} has been removed";
    //    return $"Guild memory key {key} was not found";
    //}

    //[Description("Get the user memory.")]
    //string GetUserMemory([Description("The user ID to get memory from.")] ulong userID) {
    //    if(!UserMemory.ContainsKey(userID))
    //        return "User memory is empty";

    //    var stringBuilder = new StringBuilder();
    //    stringBuilder.Append("User memory: ");
    //    foreach(var (key, value) in UserMemory[userID])
    //        stringBuilder.Append($"{key}: {value}, ");

    //    return stringBuilder.ToString();
    //}
}
