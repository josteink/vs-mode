// Guids.cs
// MUST match guids.h
using System;

namespace kjonigsennet.vsmode
{
    static class GuidList
    {
        public const string guidvsmodePkgString = "d83eb81c-8121-4fe7-9f9f-9e15a0e9836a";
        public const string guidvsmodeCmdSetString = "237da2f0-6aa6-435f-b291-0e9efbe62ef9";

        public static readonly Guid guidvsmodeCmdSet = new Guid(guidvsmodeCmdSetString);
    };
}