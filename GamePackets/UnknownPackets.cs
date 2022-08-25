﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Networking
{
    [PacketInfo(Source = PacketSource.Client, Id = 642, Length = 4, Description = "UnknownC1")]
    public class UnknownC1 : GamePacket
    {
    }

    [PacketInfo(Source = PacketSource.Client, Id = 644, Length = 0, Description = "UnknownC1")]
    public class UnknownC2 : GamePacket
    {
    }

    [PacketInfo(Source = PacketSource.Client, Id = 155, Length = 6, Description = "UnknownC1")]
    public class UnknownC3 : GamePacket
    {
        [WrappingField(SubScript = 2, Length = 2)]
        public ushort U1;
        [WrappingField(SubScript = 4, Length = 2)]
        public ushort U2;
    }

    [PacketInfo(Source = PacketSource.Client, Id = 265, Length = 11, Description = "UnknownC4")]
    public class UnknownC4 : GamePacket
    {
    }

    [PacketInfo(Source = PacketSource.Client, Id = 266, Length = 142, Description = "UnknownC5")]
    public class UnknownC5 : GamePacket
    {
    }

    [PacketInfo(Source = PacketSource.Server, Id = 691, Length = 16, Description = "UnknownS1")]
    public class UnknownS1 : GamePacket
    {
        [WrappingField(SubScript = 2, Length = 2)]
        public ushort U1;
        [WrappingField(SubScript = 4, Length = 2)]
        public ushort U2;
        [WrappingField(SubScript = 6, Length = 2)]
        public ushort U3;
        [WrappingField(SubScript = 8, Length = 2)]
        public ushort U4;
        [WrappingField(SubScript = 10, Length = 2)]
        public ushort U5;
        [WrappingField(SubScript = 12, Length = 2)]
        public ushort U6;
        [WrappingField(SubScript = 14, Length = 2)]
        public ushort U7;
    }

    [PacketInfo(Source = PacketSource.Server, Id = 692, Length = 6, Description = "UnknownS2")]
    public class UnknownS2 : GamePacket
    {
    }

    [PacketInfo(Source = PacketSource.Server, Id = 529, Length = 39, Description = "UnknownS3")]
    public class UnknownS3 : GamePacket
    {
        [WrappingField(SubScript = 2, Length = 12)]
        public byte[] U1 = new byte[] { 0, 26, 53, 0, 0, 0, 0, 0, 1, 0, 0, 1 };
        [WrappingField(SubScript = 14, Length = 4)]
        public int MapId;
        [WrappingField(SubScript = 18, Length = 4)]
        public int PlayerId;
        [WrappingField(SubScript = 22, Length = 17)]
        public byte[] U2 = new byte[] { 0, 0, 0, 0, 181, 0, 0, 0, 72, 0, 0, 0, 225, 4, 0, 0, 176 };
    }
}
