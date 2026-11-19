# Start, the Swarm!!!
docker swarm init

# Deploy the swarm based off our existing compose (WITH the compose-override, otherwise it won't work because there's some Azure keyvault stuff in that)
docker stack deploy -c docker-compose.yml -c docker-compose.override.yml -c docker-stack.yml mystack

# Cease the stack
docker stack rm mystack

# To turn off THE SWARM
docker swarm leave --force

