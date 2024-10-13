# Assistant.AI

**Assistant.AI** is a Discord bot that integrate with the [OpenAI API](https://platform.openai.com/docs/overview). When a user sends a message in any channel, or in a designated whitelisted channel, the AI intelligently decides whether to respond. If it determines that a reply is appropriate, the AI will generate and send a response to the user.
## Features

- **OpenAI API Integration**  
  Integrated with the [OpenAI API](https://platform.openai.com/docs/overview) to provide powerful AI-driven responses and interactions.
- **Commands**  
  The discord bot Includes versatile commands such as:
  - `option get/edit` for getting or updating the configuration settings for the guild.
  - `ai blacklist-user/ignore-me` to manage user access and control interaction preferences.
- **Tools Functions**  
  The application have methods called **Tools Functions** that the AI can called when needed. 
## Runing The Application

1. **Clone the repository**  
   Open your terminal and use the following command to clone the project:
   ```bash
   git clone https://github.com/AALUND13/Assistant-AI
   ```

2. **Set up the `.env` file**  
   - Navigate to the solution directory:
     ```bash
     cd Assistant-AI
     ```
   - Go to the `Assistant.AI` directory:
     ```bash
     cd Assistant.AI
     ```
   - Copy the `.env` template and rename it:
     ```bash
     cp template.env .env
     ```
   - Open the `.env` file and replace the placeholders with your actual keys:
     - Replace `DISCORD BOT TOKEN HERE` with your Discord bot token.
     - Replace `OPENAI KEY HERE` with your OpenAI API key.

3. **Open the project in Visual Studio**  
   - Go back to the solution directory:
     ```bash
     cd ../
     ```
   - Open the `.sln` (solution) file with [Visual Studio](https://visualstudio.microsoft.com).
   - In **Visual Studio**, press the **green "Run" button** or hit the `F5` key to build and run the project.