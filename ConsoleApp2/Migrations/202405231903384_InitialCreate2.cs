namespace ConsoleApp2.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate2 : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Information",
                c => new
                    {
                        InformationId = c.Int(nullable: false, identity: true),
                        CreatedDate = c.DateTime(nullable: false),
                        Latitude = c.Double(nullable: false),
                        Longitude = c.Double(nullable: false),
                        UserId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.InformationId)
                .ForeignKey("dbo.Users", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.Photos",
                c => new
                    {
                        PhotoId = c.Int(nullable: false, identity: true),
                        Path = c.String(),
                        InformationId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.PhotoId)
                .ForeignKey("dbo.Information", t => t.InformationId, cascadeDelete: true)
                .Index(t => t.InformationId);
            
            CreateTable(
                "dbo.Users",
                c => new
                    {
                        UserId = c.Int(nullable: false, identity: true),
                        Firstname = c.String(),
                        Lastname = c.String(),
                        Patronymic = c.String(),
                        PointId = c.Int(nullable: false),
                        ChatTelegramId = c.String(),
                    })
                .PrimaryKey(t => t.UserId)
                .ForeignKey("dbo.Points", t => t.PointId, cascadeDelete: true)
                .Index(t => t.PointId);
            
            CreateTable(
                "dbo.Points",
                c => new
                    {
                        PointId = c.Int(nullable: false, identity: true),
                        PointName = c.String(),
                        Adress = c.String(),
                        Latitude = c.Double(nullable: false),
                        Longitude = c.Double(nullable: false),
                    })
                .PrimaryKey(t => t.PointId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Users", "PointId", "dbo.Points");
            DropForeignKey("dbo.Information", "UserId", "dbo.Users");
            DropForeignKey("dbo.Photos", "InformationId", "dbo.Information");
            DropIndex("dbo.Users", new[] { "PointId" });
            DropIndex("dbo.Photos", new[] { "InformationId" });
            DropIndex("dbo.Information", new[] { "UserId" });
            DropTable("dbo.Points");
            DropTable("dbo.Users");
            DropTable("dbo.Photos");
            DropTable("dbo.Information");
        }
    }
}
