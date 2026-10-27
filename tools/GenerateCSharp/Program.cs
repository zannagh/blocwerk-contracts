// Copyright (c) 2026, zannagh. All rights reserved.
// See License in the project root for license information.

using GenerateCSharp;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: GenerateCSharp <schemas dir> <output dir>");
    return 2;
}

return await SchemaToCSharp.RunAsync(Path.GetFullPath(args[0]), Path.GetFullPath(args[1]));
