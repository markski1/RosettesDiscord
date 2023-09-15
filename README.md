# Rosettes
An open source Discord bot written in .NET 7, by use of Discord.NET

## Features
Learn about Rosettes' features at [https://markski.ar/rosettes](https://markski.ar/rosettes).

## What is Rosettes meant to be?

#### Simple
Rosettes should not be yet another does-it-all bot. Rosettes will never do moderation or logging stuff, it's simply meant to offer a certain level of functionality through it's commands.
The set-up process should never be any harder than inviting it into a server. At least for as long as we can get away with it.

#### Performant
Rosettes should be as quick as possible with a reasonable memory footprint. Anything that can be do in a non-blocking manner, must.

#### Privacy conscious
Rosettes will never log any personal data at all. Rosettes will never log any messages.

Only the following data is stored by Rosettes:
- User data: Name and Discord ID. Both of these are publicly available and are only kept for caching purposes. We don't even store in which Guild a specific user is in.
- Guild data stored: Name, Discord ID, Owner's Discord ID (see above), Member count and Roles. Again, all this data is already public, and kept for caching and admin-panel functionality purposes. Furthermore, you can request all this data be automatically deleted from the admin panel if you ever decide to get rid of Rosettes.
- Obvious data stored: Autoroles, Settings, Alarms (deleted after they ring) and Polls (For the sake of preventing double-voting, we will store IF someone voted in a poll, but we will not store WHAT they voted for - all poll replies are anonymous).

## What is Rosettes not meant to be?

#### A template for your bot
Rosettes should not be used as a template for other bots. The main reason I made this bot in .NET was to learn .NET, which is to say, I began writing this bot with absolutely 0 C# or .NET knoweldge.
Having worked in software development for a while though, I like to think I've been doing a good work with the code despite my inexperience in this language, but it's most likely not the best it could be.

#### Annoying
Rosettes' replies to queries and patterns it finds should be as short and consise as possible.
Within reason, it should not be spammy.

## Building
The solution file is there, all you have to do is open and compile.

Getting it to work is a different beast. Rosettes was not built to be easily portable/deployed.

If you really want to try:
- the Database folder has a .sql file with the database schema
- Settings.cs within the Core folder should tell you where to play the necesary tokens (within .txt files, no line breaks).
- mysql.txt specifically should look like this:

```
{
	"Server": "host.name",
	"UserID": "username",
	"Password": "password",
	"Database": "table_name"
}
```

The reason I open sourced Rosettes is a simple matter of transparency. Let people see for themselves how things run behind the scenes. It's not really meant to be used on your own.

## LICENCE

Anyone, anywhere, for any reason, can use any of the code in this repository.
It is provided as-is, I am not responsible or liable for anything related to it's use by third parties.
If the code is used in a public-facing application, I'd appreciate to be given some minimal credit somewhere, but I certainly won't bother enforcing this if you wish to not do it.
