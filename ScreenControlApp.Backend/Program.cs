using Microsoft.AspNetCore.Cors.Infrastructure;
using ScreenControlApp.Backend.Hubs;
using ScreenControlApp.Backend.Services;

namespace ScreenControlApp.Backend {
	public class Program {
		public static void Main(string[] args) {
			var builder = WebApplication.CreateBuilder(args);

			// Add services to the container.

			builder.Services.AddControllers();
			builder.Services.AddHostedService<HostIdCleanupService>();
			builder.Services.AddSignalR();
			builder.Services.AddCors(options => {
				options.AddPolicy("CorsPolicy",
					builder => builder
					.AllowAnyOrigin()
					.AllowAnyMethod()
					.AllowAnyHeader()
					);

				options.AddPolicy("signalr",
					builder => builder
					.AllowAnyMethod()
					.AllowAnyHeader()

					.AllowCredentials()
					.SetIsOriginAllowed(hostName => true));
			});
			// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
			//builder.Services.AddEndpointsApiExplorer();
			//builder.Services.AddSwaggerGen();

			var app = builder.Build();

			// Configure the HTTP request pipeline.
			//if (app.Environment.IsDevelopment()) {
			//	app.UseSwagger();
			//	app.UseSwaggerUI();
			//}

			//app.UseHttpsRedirection();

			app.UseAuthorization();

			app.MapControllers();
			app.MapHub<ScreenControlHub>("/screenControlHub");
			//app.UseCors("CorsPolicy");
			app.UseCors("signalr"); //THIS FUCKING FINALLY WORKS!!!
			app.Run();
		}
	}
}
