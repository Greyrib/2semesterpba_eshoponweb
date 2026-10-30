This project only knows how to connect to RabbitMQ and pass messages around. It knows nothing about catalogs, orders, or any business concept.

Every service (monolith, CatalogMicroservice, future microservices) references this one project instead of writing its own RabbitMQ connection code.

Each service defines its own message types locally — these are not shared. Copying a small record into each service is intentional, so services can change independently without needing to redeploy together.

Sharing this plumbing is fine specifically because it carries no domain knowledge. Sharing message contracts would not be fine, since that would force services to agree on and update together.