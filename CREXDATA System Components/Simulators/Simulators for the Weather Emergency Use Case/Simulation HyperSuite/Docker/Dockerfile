FROM node:18-alpine

# Set working directory
WORKDIR /app

# Copy package files and install dependencies
COPY package*.json ./
RUN npm install

# Copy the rest of the project
COPY . .

# Expose port 5173
EXPOSE 5173

# Start the development server
CMD ["npm", "run", "serve", "--", "--host", "0.0.0.0", "--port", "5173"]
