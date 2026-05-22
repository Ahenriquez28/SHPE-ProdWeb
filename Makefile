# ─── Docker ─────────────────────────────────────────────

# Start the full stack (Postgres + API)
up:
	docker compose up --build

# Stop all containers
down:
	docker compose down

# View live logs from all containers
logs:
	docker compose logs -f

# ─── Database ───────────────────────────────────────────

# Create a new migration (usage: make migrate-add name=AddEventTable)
migrate-add:
	docker compose exec api dotnet ef migrations add $(name)

# Apply all pending migrations
migrate-up:
	docker compose exec api dotnet ef database update

# Revert the last migration
migrate-down:
	docker compose exec api dotnet ef migrations remove

# Drop into a live psql shell to inspect the database
psql:
	docker compose exec postgres psql -U postgres -d shpe_dev