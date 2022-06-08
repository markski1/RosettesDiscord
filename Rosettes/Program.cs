/*
 *  Program
 *  
 *  This is the entry point for the Rosettes process.
 *  All it does is invoke the MainAsync method at StartRosettes.
 *  Then it just stays stuck in await hell until Rosettes stops running.
 *  
 */
using Rosettes.core;

await Global.RosettesMain.MainAsync();