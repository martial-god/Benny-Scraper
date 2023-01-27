﻿// <auto-generated />
using System;
using Benny_Scraper.DataAccess.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace BennyScraper.DataAccess.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20230127061900_ChangeChapterNumber")]
    partial class ChangeChapterNumber
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("Benny_Scraper.Models.Chapter", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier")
                        .HasColumnName("id");

                    b.Property<string>("Content")
                        .HasColumnType("nvarchar(max)")
                        .HasColumnName("content");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("datetime2")
                        .HasColumnName("date_created");

                    b.Property<Guid>("NovelId")
                        .HasColumnType("uniqueidentifier")
                        .HasColumnName("novel_id");

                    b.Property<string>("Number")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)")
                        .HasColumnName("number");

                    b.Property<string>("Title")
                        .HasMaxLength(255)
                        .HasColumnType("nvarchar(255)")
                        .HasColumnName("title");

                    b.Property<string>("Url")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)")
                        .HasColumnName("url");

                    b.HasKey("Id");

                    b.HasIndex("NovelId");

                    b.ToTable("chapter", (string)null);
                });

            modelBuilder.Entity("Benny_Scraper.Models.Novel", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier")
                        .HasColumnName("id");

                    b.Property<string>("Author")
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)")
                        .HasColumnName("author");

                    b.Property<string>("CurrentChapter")
                        .IsRequired()
                        .HasMaxLength(144)
                        .HasColumnType("nvarchar(144)")
                        .HasColumnName("current_chapter");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("datetime2")
                        .HasColumnName("date_created");

                    b.Property<string>("Description")
                        .HasColumnType("nvarchar(max)")
                        .HasColumnName("description");

                    b.Property<string>("FirstChapter")
                        .IsRequired()
                        .HasMaxLength(144)
                        .HasColumnType("nvarchar(144)")
                        .HasColumnName("first_chapter");

                    b.Property<string>("Genre")
                        .HasColumnType("nvarchar(max)")
                        .HasColumnName("genre");

                    b.Property<bool>("LastChapter")
                        .HasColumnType("bit")
                        .HasColumnName("last_chapter");

                    b.Property<string>("SaveLocation")
                        .HasMaxLength(255)
                        .HasColumnType("nvarchar(255)")
                        .HasColumnName("save_location");

                    b.Property<string>("SiteName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)")
                        .HasColumnName("site_name");

                    b.Property<string>("Status")
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)")
                        .HasColumnName("status");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("nvarchar(255)")
                        .HasColumnName("title");

                    b.Property<int?>("TotalChapters")
                        .HasColumnType("int")
                        .HasColumnName("total_chapters");

                    b.Property<string>("Url")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)")
                        .HasColumnName("url");

                    b.HasKey("Id");

                    b.ToTable("novel", (string)null);
                });

            modelBuilder.Entity("Benny_Scraper.Models.NovelList", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<bool>("Completed")
                        .HasColumnType("bit");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("datetime2");

                    b.Property<string>("Description")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("NovelId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("nvarchar(255)");

                    b.HasKey("Id");

                    b.HasIndex("NovelId");

                    b.ToTable("NovelLists");
                });

            modelBuilder.Entity("Benny_Scraper.Models.Chapter", b =>
                {
                    b.HasOne("Benny_Scraper.Models.Novel", "Novel")
                        .WithMany("Chapters")
                        .HasForeignKey("NovelId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Novel");
                });

            modelBuilder.Entity("Benny_Scraper.Models.NovelList", b =>
                {
                    b.HasOne("Benny_Scraper.Models.Novel", "Novel")
                        .WithMany()
                        .HasForeignKey("NovelId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Novel");
                });

            modelBuilder.Entity("Benny_Scraper.Models.Novel", b =>
                {
                    b.Navigation("Chapters");
                });
#pragma warning restore 612, 618
        }
    }
}
