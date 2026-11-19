# Start, the Swarm!!!
docker swarm init

# Deploy the swarm based off our existing compose (WITH the compose-override, otherwise it won't work because there's some Azure keyvault stuff in that)
docker stack deploy -c docker-compose.yml -c docker-compose.override.yml -c docker-compose.swarm.yml mystack

# Helper: If you want to see what the output of the stack above becomes, you can do this variant:
docker compose -f docker-compose.yml -f docker-compose.override.yml -f docker-compose.swarm.yml config

# Cease the stack
docker stack rm mystack

# To turn off THE SWARM
docker swarm leave --force

