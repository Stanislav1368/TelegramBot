namespace ConsoleApp2
{
    using System;
    using System.Collections.Generic;

    public partial class Information
    {
        public Information()
        {
            this.Photos = new HashSet<Photo>();
        }

        public int InformationId { get; set; }
        public DateTime CreatedDate { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int UserId { get; set; }

        public virtual User User { get; set; }
        public virtual ICollection<Photo> Photos { get; set; }
    }

    public partial class Photo
    {
        public int PhotoId { get; set; }
        public string Path { get; set; }
        public int InformationId { get; set; }

        public virtual Information Information { get; set; }
    }

    public partial class User
    {
        public User()
        {
            this.Informations = new HashSet<Information>();
        }

        public int UserId { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public string Patronymic { get; set; }
        public int PointId { get; set; }
        public string ChatTelegramId { get; set; }

        public virtual Point Point { get; set; }
        public virtual ICollection<Information> Informations { get; set; }
    }

    public partial class Point
    {
        public Point()
        {
            this.Users = new HashSet<User>();
        }

        public int PointId { get; set; }
        public string PointName { get; set; }
        public string Adress { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public virtual ICollection<User> Users { get; set; }
    }
}
