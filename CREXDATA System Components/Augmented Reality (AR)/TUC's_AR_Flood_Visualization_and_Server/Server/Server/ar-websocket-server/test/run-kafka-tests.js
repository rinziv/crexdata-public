#!/usr/bin/env node

/**
 * Simple test runner script for Kafka publisher tests
 * This script provides an easy way to run the Kafka tests with different options
 */

const path = require("path");
const { spawn } = require("child_process");

const COLORS = {
  reset: "\x1b[0m",
  bright: "\x1b[1m",
  red: "\x1b[31m",
  green: "\x1b[32m",
  yellow: "\x1b[33m",
  blue: "\x1b[34m",
  cyan: "\x1b[36m",
};

function colorize(text, color) {
  return `${COLORS[color]}${text}${COLORS.reset}`;
}

function printUsage() {
  console.log(colorize("\n📋 Kafka Test & Interactive Runner", "cyan"));
  console.log(colorize("===================================", "cyan"));
  console.log("\nUsage: node run-kafka-tests.js [command] [options]\n");

  console.log(colorize("Test Commands:", "yellow"));
  console.log("  publish-only        Run publisher-only test (no consumer verification)");
  console.log("  full-test           Run complete test with optional consumer verification");
  console.log("  jest                Run Jest unit tests");

  console.log(colorize("\nInteractive Commands:", "yellow"));
  console.log("  consumer            Start interactive consumer (live message reading)");
  console.log("  interactive         Start interactive producer-consumer manager");
  console.log("  manager             Alias for interactive");

  console.log(colorize("\nUtility Commands:", "yellow"));
  console.log("  help                Show this help message\n");

  console.log(colorize("Examples:", "green"));
  console.log("  node run-kafka-tests.js publish-only");
  console.log("  node run-kafka-tests.js consumer");
  console.log("  node run-kafka-tests.js interactive");
  console.log("  npm run test:kafka:publish");
  console.log("  npm run test:kafka:consumer");
  console.log("  npm run test:kafka:interactive\n");
}

function runCommand(command, args = [], options = {}) {
  return new Promise((resolve, reject) => {
    console.log(colorize(`🚀 Running: ${command} ${args.join(" ")}`, "blue"));

    const child = spawn(command, args, {
      stdio: "inherit",
      shell: true,
      ...options,
    });

    child.on("close", (code) => {
      if (code === 0) {
        console.log(colorize(`✅ Command completed successfully`, "green"));
        resolve(code);
      } else {
        console.log(colorize(`❌ Command failed with exit code ${code}`, "red"));
        reject(new Error(`Command failed with exit code ${code}`));
      }
    });

    child.on("error", (error) => {
      console.error(colorize(`💥 Failed to run command: ${error.message}`, "red"));
      reject(error);
    });
  });
}

async function runPublisherOnlyTest() {
  console.log(colorize("\n🧪 Starting Publisher-Only Test", "cyan"));
  console.log(colorize("===============================", "cyan"));

  const testFile = path.join(__dirname, "kafka-publisher-test.js");
  await runCommand("node", [testFile, "--publish-only"]);
}

async function runFullTest() {
  console.log(colorize("\n🧪 Starting Full Test Suite", "cyan"));
  console.log(colorize("=========================", "cyan"));

  const testFile = path.join(__dirname, "kafka-publisher-test.js");
  await runCommand("node", [testFile]);
}

async function runJestTests() {
  console.log(colorize("\n🧪 Starting Jest Unit Tests", "cyan"));
  console.log(colorize("==========================", "cyan"));

  // Check if jest is available
  try {
    await runCommand("npx", ["jest", "--version"]);
  } catch (error) {
    console.log(colorize("\n⚠️  Jest not found. Installing jest...", "yellow"));
    await runCommand("npm", ["install", "--save-dev", "jest"]);
  }

  const testFile = path.join(__dirname, "kafka-publisher.test.js");
  await runCommand("npx", ["jest", testFile, "--verbose"]);
}

async function runInteractiveConsumer() {
  console.log(colorize("\n👂 Starting Interactive Consumer", "cyan"));
  console.log(colorize("================================", "cyan"));

  const consumerFile = path.join(__dirname, "kafka-interactive-consumer.js");
  await runCommand("node", [consumerFile]);
}

async function runInteractiveManager() {
  console.log(colorize("\n🎯 Starting Interactive Producer-Consumer Manager", "cyan"));
  console.log(colorize("=================================================", "cyan"));

  const managerFile = path.join(__dirname, "kafka-interactive-manager.js");
  await runCommand("node", [managerFile]);
}

async function main() {
  const args = process.argv.slice(2);
  const command = args[0];

  try {
    switch (command) {
      case "publish-only":
      case "publisher":
      case "pub":
        await runPublisherOnlyTest();
        break;

      case "full-test":
      case "full":
      case "complete":
        await runFullTest();
        break;

      case "jest":
      case "unit":
      case "unit-tests":
        await runJestTests();
        break;

      case "consumer":
      case "interactive-consumer":
        await runInteractiveConsumer();
        break;

      case "interactive":
      case "manager":
      case "interactive-manager":
        await runInteractiveManager();
        break;

      case "help":
      case "--help":
      case "-h":
        printUsage();
        break;

      default:
        if (!command) {
          console.log(colorize("⚠️  No command specified. Showing help...", "yellow"));
        } else {
          console.log(colorize(`❌ Unknown command: ${command}`, "red"));
        }
        printUsage();
        process.exit(1);
    }

    console.log(colorize("\n🎉 Test runner completed successfully!", "green"));
  } catch (error) {
    console.error(colorize(`\n💥 Test runner failed: ${error.message}`, "red"));
    process.exit(1);
  }
}

// Run the main function if this script is executed directly
if (require.main === module) {
  main();
}

module.exports = {
  runPublisherOnlyTest,
  runFullTest,
  runJestTests,
  runInteractiveConsumer,
  runInteractiveManager,
  runCommand,
};
