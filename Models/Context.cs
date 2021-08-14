using Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace API.Models
{
    public class Context : IdentityDbContext<AppUser>
    {
        public Context(DbContextOptions<Context> options) : base(options)
        {
        }

        public DbSet<Message> Messages { get; set; }
        public DbSet<Recipient> Recipients { get; set; }
        public DbSet<FriendRequest> FriendRequests { get; set; }
        public DbSet<Friend> Friends { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<File> Files { get; set; }
        public DbSet<Image> Images { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<Conversation>().HasMany(t => t.Messages).WithOne(t => t.Conversation);
            builder.Entity<Conversation>().HasMany(t => t.Recipients).WithOne(t => t.Conversation);
            builder.Entity<Conversation>().HasMany(t => t.Files).WithOne(t => t.Conversation);
            builder.Entity<Friend>().HasOne(t => t.Conversation).WithOne(t => t.Friend).HasForeignKey<Conversation>(t => t.FriendId);
            builder.Entity<Image>().Property(i => i.Id).ValueGeneratedNever();
            builder.Entity<File>().Property(i => i.Id).ValueGeneratedNever();

        }

    }
}