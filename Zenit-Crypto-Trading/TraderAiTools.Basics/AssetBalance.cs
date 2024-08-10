using System;

namespace Hallupa.TraderTools.Basics
{
    public class AssetBalance
    {
        public AssetBalance(string asset, decimal balance)
        {
            Asset = asset;
            Balance = balance;
        }

        public string Asset { get; private set; }
        public decimal Balance { get; private set; }

        public override string ToString()
        {
            return $"{Balance:0.000} {Asset}";
        }
    }
}