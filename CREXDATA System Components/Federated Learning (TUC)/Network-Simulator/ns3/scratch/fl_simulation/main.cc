#include <random>
#include <chrono>
#include <vector>
#include <iostream>
#include <unistd.h>
#include <fstream>
#include "fl-experiment.h"
#include "utils.h"
#include <sys/stat.h>
#include "ns3/core-module.h"
#include "fl-client-session.h"
#include <filesystem>
#include <sstream>
#include <iomanip>
#include <functional>


namespace fs = std::filesystem;

using sysclock_t = std::chrono::system_clock;
using namespace ns3;
using namespace std;

std::map<int, std::shared_ptr<ClientSession>> g_clients;

NS_LOG_COMPONENT_DEFINE("Federated-Learning");

void createExperimentDir(const std::string &dirName)
{
  if (!fs::exists(dirName))
  {
    fs::create_directories(dirName);
    std::cout << "[main.cc] Created output directory: " << dirName << std::endl;
  }
}

void saveRoundResults(const std::string &experimentDir, int roundNo,
                      const std::map<int, Experiment::RoundStats> &roundStats)
{
  std::string roundFile = experimentDir + "/round_" + std::to_string(roundNo) + ".csv";

  std::ofstream file(roundFile);
  if (!file.is_open())
  {
    std::cerr << "[main.cc] Could not open file for writing: " << roundFile << std::endl;
    return;
  }

  file << "round,id,connectionTime,downlinkTime,monitorigTime,uplinkTime,throughput,routerDistance\n";

  for (const auto &stat : roundStats)
  {
    int clientId = stat.first;
    const Experiment::RoundStats &result = stat.second;
    file << roundNo << "," << clientId << "," << std::fixed << std::setprecision(2)
         << result.connectionTime << "," << result.downlinkTime << ","
         << result.monitoringTime << "," << result.uplinkTime << ","
         << result.throughput << "," << result.routerDistance << "\n";
  }

  file.close();
  std::cout << "[main.cc] Round results saved to " << roundFile << std::endl;
}

int main(int argc, char *argv[])
{
  ns3::CommandLine cmd;

  int roundNo = 0;
  std::string experimentDir = "results/";
  int monitorMsgSize = 5;
  bool forceLog = false;
  int numClients = 10;
  std::string networkType = "wifi";
  int maxPacketSize = 1024;
  double TxGain = 0.0;
  double modelSize = 1.0;
  bool clientMobility = false;
  std::string serverDatarate = "100Mbps";
  int networkStandard = 0;

  cmd.AddValue("roundNo", "The simulated round's number", roundNo);
  cmd.AddValue("forceLog", "Force logging even if file exists", forceLog);
  cmd.AddValue("monitorMsgSize", "The size of monitoring message", monitorMsgSize);
  cmd.AddValue("experimentDir", "The name of the folder to store the output", experimentDir);
  cmd.AddValue("networkType", "Type of network (e.g., wifi, ethernet)", networkType);
  cmd.AddValue("numClients", "Number of clients", numClients);
  cmd.AddValue("maxPacketSize", "Maximum packet size in bytes", maxPacketSize);
  cmd.AddValue("txGain", "Transmission gain in dB", TxGain);
  cmd.AddValue("modelSize", "Model size in Mb", modelSize);
  cmd.AddValue("clientMobility", "Enable or disable client mobility", clientMobility);
  cmd.AddValue("serverDatarate", "Data rate for CSMA", serverDatarate);
  cmd.AddValue("networkStandard", "WiFi network template (speed)", networkStandard);
  cmd.Parse(argc, argv);
  std::cout << "[main.cc] Starting NS3 Round Simulation" << std::endl;

  modelSize *= 1'000'000;
  if (monitorMsgSize>maxPacketSize){
    maxPacketSize=monitorMsgSize;
  }
  RngSeedManager::SetSeed(roundNo);

  createExperimentDir(experimentDir);

  std::string roundFile = experimentDir + "/round_" + std::to_string(roundNo) + ".csv";
  if (fs::exists(roundFile) && !forceLog)
  {
    std::cout << "[main.cc] Skipping, result already exists for current configuration and round No." << std::endl;
    return 0;
  }

  string COORDINATES_FILE = "scratch/fl_simulation/conf/coordinates.txt";
  
  vector<vector<double>> coord_array = readCoordinates(COORDINATES_FILE, numClients + 1);

  int server = 0;
  for (int j = 0; j < numClients; j++)
  {
    int x = coord_array[j + 1][0];
    int y = coord_array[j + 1][1];
    g_clients[j] = std::shared_ptr<ClientSession>(new ClientSession(j, x, y));
  }
  

  std::string PARTICIPATION_FILE = "scratch/fl_simulation/conf/steps.csv";
  std::string overridePath = experimentDir + "/steps.csv";

  if (fs::exists(overridePath))
  {
      PARTICIPATION_FILE = overridePath;
  }

  int monitorSteps = loadParticipationCSV(PARTICIPATION_FILE, roundNo, g_clients);

  std::cout << "[main.cc] Parsed round configuration. Executing simulation..." << std::endl;

  auto experiment = Experiment(networkStandard, clientMobility, networkType, maxPacketSize, TxGain, serverDatarate,
                              modelSize, coord_array[server], roundNo);

  auto roundStats = experiment.ExecRoundCom(g_clients, monitorSteps, monitorMsgSize);
  std::cout << "[main.cc] Round Simulation completed successfully!"  << std::endl;

  saveRoundResults(experimentDir, roundNo, roundStats);

  std::cout << "[main.cc] Simulation complete." << std::endl;
  return 0;
}
