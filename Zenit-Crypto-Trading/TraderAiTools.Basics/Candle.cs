using System;
using System.Runtime.InteropServices;

namespace TraderTools.Basics
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Candle
    {
        public long OpenTimeTicks { get; set; }
        public long CloseTimeTicks { get; set; }
        public float OpenBid { get; set; }
        public float CloseBid { get; set; }
        public float HighBid { get; set; }
        public float LowBid { get; set; }
        public float OpenAsk { get; set; }
        public float CloseAsk { get; set; }
        public float HighAsk { get; set; }
        public float LowAsk { get; set; }
        public float Volume { get; set; }
        public byte IsComplete { get; set; }

        public override string ToString()
        {
            return $"OpenTime: {new DateTime(OpenTimeTicks)} CloseTime: {new DateTime(CloseTimeTicks)} Open bid: {OpenBid} Close bid: {CloseBid} Volume: {Volume}";
        }
    }
}