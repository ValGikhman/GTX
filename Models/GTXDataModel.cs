namespace GTX.Models {

    public class Employer {
        public int id { get; set; }

        public string Name { get; set; }

        public string Position { get; set; }

        public string Phone { get; set; }

        public string Email { get; set; }
    }

    public class OpenHours{
        public string Day { get; set; }

        public string From { get; set; }

        public string To { get; set; }

        public string Description { get; set; }
    }
}