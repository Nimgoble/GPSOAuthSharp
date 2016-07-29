# GPSOAuthSharp
A Portable .NET client library for Google Play Services OAuth written in C#.

This is a C# port of https://github.com/simon-weber/gpsoauth

## NuGet package
You can find this on NuGet at https://www.nuget.org/packages/GPSOAuthSharp/

## Usage
Construct a `GPSOAuthSharp.GPSOAuthClient(email, password)`.

Use `PerformMasterLogin()` or `PerformOAuth()` to retrieve a `Dictionary<string, string>` of response values. 

Demo response values: 

![](http://i.imgur.com/v5PqdKe.png)

Wrong credentials:

![](http://i.imgur.com/ubakOF3.png)

You can download an executable for the Demo on the [Releases page](https://github.com/vemacs/GPSOAuthSharp/releases/). 

The source for the Demo is located in the `GPSOAuthDemo` directory. The [main class](https://github.com/vemacs/GPSOAuthSharp/blob/master/GPSOAuthDemo/GPSOAuthDemo/Program.cs) is here.

Python result (for comparison): 

![](http://i.imgur.com/JyLnAK5.png)

## Goals
This project intends to follow the Google-specific parts of the Python implementation extremely carefully, so that any changes made to the Python implementation can be easily applied to this.
