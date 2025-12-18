# Installation

This guide covers installing SimpleDiscordNet in your .NET project.

## Table of Contents
- [Prerequisites](#prerequisites)
- [NuGet Installation (Recommended)](#nuget-installation-recommended)
- [Source Reference (Advanced)](#source-reference-advanced)
- [Verifying Installation](#verifying-installation)

---

## Prerequisites

Before installing SimpleDiscordNet, ensure you have:

- **.NET SDK 10.0 or newer** - [Download here](https://dotnet.microsoft.com/download/dotnet/10.0)
- **C# 14** (included with .NET 10 SDK)
- **Discord Bot Token** - [Create a bot](https://discord.com/developers/applications)

### Checking Your .NET Version

```bash
dotnet --version
```

You should see `10.0.x` or higher.

---

## NuGet Installation (Recommended)

The easiest way to install SimpleDiscordNet is via NuGet. The package includes both the runtime library and the source generator.

### Using .NET CLI

```bash
dotnet add package SimpleDiscordDotNet
```

### Using Package Manager Console (Visual Studio)

```powershell
Install-Package SimpleDiscordDotNet
```

### Using Visual Studio GUI

1. Right-click your project in Solution Explorer
2. Select **Manage NuGet Packages**
3. Search for **SimpleDiscordDotNet**
4. Click **Install**

### Manually Editing .csproj

Add this to your project file:

```xml
<ItemGroup>
  <PackageReference Include="SimpleDiscordDotNet" Version="*" />
</ItemGroup>
```

**Note:** Replace `*` with a specific version number for production apps.

---

## Source Reference (Advanced)

For contributors or if you need to modify the library locally, you can reference the projects directly.

### Important

You must include **BOTH** projects:
- `SimpleDiscordNet.csproj` - Runtime library
- `SimpleDiscordNet.Generators.csproj` - Source generator

Without the generator, command/component discovery won't work.

### Clone the Repository

```bash
git clone https://github.com/SimpleDiscordNet/SimpleDiscordNet.git
```

### Add to Your Solution (.NET CLI)

```bash
# Add both projects to your solution
dotnet sln add .\SimpleDiscordNet\SimpleDiscordNet\SimpleDiscordNet.csproj
dotnet sln add .\SimpleDiscordNet\SimpleDiscordNet.Generators\SimpleDiscordNet.Generators.csproj

# Reference both projects from your app
dotnet add <YourApp.csproj> reference .\SimpleDiscordNet\SimpleDiscordNet\SimpleDiscordNet.csproj
dotnet add <YourApp.csproj> reference .\SimpleDiscordNet\SimpleDiscordNet.Generators\SimpleDiscordNet.Generators.csproj
```

### Add to Your Solution (Visual Studio)

1. **File → Add → Existing Project**
   - Add `SimpleDiscordNet/SimpleDiscordNet/SimpleDiscordNet.csproj`
   - Add `SimpleDiscordNet/SimpleDiscordNet.Generators/SimpleDiscordNet.Generators.csproj`

2. Right-click your app project → **Add → Project Reference**
   - Check both **SimpleDiscordNet** and **SimpleDiscordNet.Generators**

3. Build the solution to trigger source generation

---

## Verifying Installation

### 1. Check Build Output

Build your project:

```bash
dotnet build
```

You should see no errors related to SimpleDiscordNet.

### 2. Test Import

Create a test file:

```csharp
using SimpleDiscordNet;
using SimpleDiscordNet.Commands;
using SimpleDiscordNet.Primitives;

Console.WriteLine("SimpleDiscordNet is installed!");
```

If there are no errors, the installation is successful.

### 3. Verify Source Generator

The source generator should automatically discover commands and components. You'll know it's working when your `[SlashCommand]` and `[ComponentHandler]` attributes work correctly at runtime.

---

## Project Structure

After installation, your project should look like this:

```
YourBotProject/
├── YourBotProject.csproj (references SimpleDiscordDotNet)
├── Program.cs (bot startup code)
└── Commands/
    ├── BasicCommands.cs
    └── ... (your command files)
```

---

## Next Steps

Now that SimpleDiscordNet is installed:

1. **[Create your first bot](Getting-Started)** - Follow the getting started guide
2. **[Configure Discord Application](Getting-Started#setting-up-your-discord-bot)** - Get your bot token and intents
3. **[Write slash commands](Slash-Commands)** - Add functionality to your bot

---

## Troubleshooting

### Package Not Found

If NuGet can't find the package:

```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Try installing again
dotnet add package SimpleDiscordDotNet
```

### Source Generator Not Working

If commands aren't being discovered:

1. Clean and rebuild:
   ```bash
   dotnet clean
   dotnet build
   ```

2. Verify both projects are referenced (for source reference installs)

3. Check that your command classes are marked with `[SlashCommand]` attributes

4. Ensure your project targets `net10.0`

### Version Conflicts

If you get version conflicts:

```xml
<ItemGroup>
  <PackageReference Include="SimpleDiscordDotNet" Version="1.0.3" />
</ItemGroup>
```

Specify an explicit version number.

---

**Need help?** Check the [FAQ](FAQ) or open an issue on [GitHub](https://github.com/SimpleDiscordNet/SimpleDiscordNet/issues).
