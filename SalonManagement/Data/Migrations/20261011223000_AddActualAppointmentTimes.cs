using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace SalonManagement.Data.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261011223000_AddActualAppointmentTimes")]
public partial class AddActualAppointmentTimes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder){migrationBuilder.AddColumn<DateTime>(name:"ActualStartedAt",table:"Appointments",type:"datetime2",nullable:true);migrationBuilder.AddColumn<DateTime>(name:"ActualCompletedAt",table:"Appointments",type:"datetime2",nullable:true);}
    protected override void Down(MigrationBuilder migrationBuilder){migrationBuilder.DropColumn(name:"ActualStartedAt",table:"Appointments");migrationBuilder.DropColumn(name:"ActualCompletedAt",table:"Appointments");}
}
