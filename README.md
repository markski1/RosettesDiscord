# Rosettes
A simple Discord bot written in .NET 6 through Discord.NET

## What is Rosettes meant to be?

#### Simple
Rosettes should not be yet another does-it-all bot. Rosettes will never do moderation or logging stuff, it's simply meant to offer a certain level of functionality through it's commands.
The set-up process should never be any harder than inviting it into a server. At least for as long as we can get away with it.

#### Performant
Rosettes should be as quick as possible with a reasonable memory footprint. Anything that can be do in a non-blocking manner, must.

#### Privacy conscious
Rosettes must never log any personal data at all. The only things stored are alarms, experience and "currency" numbers for users, using their ID for relationality, which is publicly available anyways.

## What is Rosettes not meant to be?

#### A template for your bot
Rosettes should not be used as a template for other bots. The main reason I made this bot in .NET was to learn .NET, which is to say, I began writing this bot with absolutely 0 C# or .NET knoweldge.
Having worked in software development for a while though, I like to think I've been doing a good work with the code despite my inexperience in this language, but it's most likely not the best it could be.

#### Annoying
Rosettes' replies to queries and patterns it finds should be as short and consise as possible.
Within reason, it should not be spammy.

## Building
The solution file is there, all you have to do is open and compile.

Getting it to work is a different beast. Rosettes was not built to be easily portable/deployed. There are token keys and database tables required for stuff to work.
The reason I open sourced Rosettes is a simple matter of transparency. Let people see for themselves how things run behind the scenes. It's not really meant to be used on your own.

You are, of course, free to grab any source code here and use it as you please, but don't expect to receive support if stuff doesn't work first try.
