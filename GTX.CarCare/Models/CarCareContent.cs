using System.Collections.Generic;

namespace GTX.CarCare.Models
{
    public static class CarCareContent
    {
        public const string BusinessName = "GTX Car Care Center";
        public const string Phone = "(513) 489-2886";
        public const string PhoneLink = "tel:+15134892886";

        public static readonly IReadOnlyList<CarCareService> Services = new List<CarCareService>
        {
            new CarCareService("tire-change", "01", "Tire changes", "A fresh start where the road begins.",
                "Time for replacement tires? Talk with us about a tire change for your vehicle and the way you drive.",
                "Have your vehicle's year, make, model, and tire size handy when you call."),
            new CarCareService("oil-change", "02", "Oil changes", "Keep the everyday miles moving.",
                "Make routine maintenance part of your rhythm. Contact us to discuss the right oil-change service for your vehicle.",
                "Tell us your current mileage and when your oil was last changed, if you know."),
            new CarCareService("alignment", "03", "Wheel alignments", "Get back on the straight and narrow.",
                "Noticing uneven tire wear or a steering wheel that sits off-center? Ask us about checking your wheel alignment.",
                "Describe what you are noticing so we can help you plan your visit."),
            new CarCareService("detailing", "04", "Car detailing", "A little more attention. A whole new feeling.",
                "Give your vehicle some extra care, inside and out. Call to discuss detailing options and the areas you want us to focus on.",
                "Let us know your vehicle size and whether you need interior care, exterior care, or both."),
            new CarCareService("car-wash", "05", "Car washes", "Leave the daily grime behind.",
                "Freshen up your ride with a car wash. Get in touch to ask about available wash options and plan a visit.",
                "Call for current availability and to find the right option for your vehicle.")
        }.AsReadOnly();
    }

    public sealed class CarCareService
    {
        public CarCareService(string id, string number, string name, string tagline, string description, string preparation)
        {
            Id = id;
            Number = number;
            Name = name;
            Tagline = tagline;
            Description = description;
            Preparation = preparation;
        }

        public string Id { get; private set; }
        public string Number { get; private set; }
        public string Name { get; private set; }
        public string Tagline { get; private set; }
        public string Description { get; private set; }
        public string Preparation { get; private set; }
    }
}
