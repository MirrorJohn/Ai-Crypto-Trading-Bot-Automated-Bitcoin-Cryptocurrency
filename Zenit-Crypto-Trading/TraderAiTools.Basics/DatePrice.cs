using System;

namespace TraderTools.Basics
{
    public class DatePrice
    {
        public DatePrice(string id, DateTime date, decimal? price)
        {
            Id = id;
            Date = date;
            Price = price;
        }

        public DatePrice(DateTime date, decimal? price)
        {
            Date = date;
            Price = price;
        }

        public DatePrice()
        {
        }

        public string Id { get; set; }
        public DateTime Date { get; set; }
        public decimal? Price { get; set; }

        public override string ToString()
        {
            return $"{Date} {Price}";
        }
    }
}