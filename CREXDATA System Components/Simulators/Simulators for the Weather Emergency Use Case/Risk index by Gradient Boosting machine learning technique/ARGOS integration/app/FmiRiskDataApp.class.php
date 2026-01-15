<?php

declare(strict_types=1);

class FmiRiskDataApp extends Application
{
    use TimeSeriesTrait;
    private const string TIME_ZONE = "UTC";

    /**
     * @throws Exception
     */
    public function run(): void
    {
        Log::getInstance()->info("Requesting available files...");
        $available_files = $this->listAvailableFiles();
        if (empty($available_files)) {
            Log::getInstance()->info("No available files to download");
            return;
        }

        [
            "simulation" => $simulation,
            "files" => $files_to_download
        ] = $this->getMostRecentFiles($available_files);
        Log::getInstance()->info("Latest simulation: " . $simulation->format(DateTimeImmutable::ATOM));
        $simulation_epoch = $simulation->getTimestamp();

        $products = $this->loadProducts($this->config->products, include_elements: true);
        $last_processed_simulations = array_map(static fn ($prod_conf) => $prod_conf->details->last_simulation, $products);

        $should_download = empty($last_processed_simulations) || $simulation_epoch > max($last_processed_simulations);
        if (!$should_download) {
            Log::getInstance()->info("Already processed");
            return;
        }

        Log::getInstance()->info("Downloading files...");
        $output_dir = FileSystemUtils::createTempDir(prefix: self::class);
        try {
            $this->downloadFiles($files_to_download, $output_dir);
            $csv_files = glob($output_dir . "/*.csv");
            if (empty($csv_files)) {
                Log::getInstance()->warning("No files were downloaded");
                return;
            }

            foreach ($products as $prod_conf) {
                Log::getInstance()->info("Processing [$prod_conf->variable]");
                $product = $prod_conf->details;

                if (!isset($prod_conf->code)) {
                    Log::getInstance()->warning("Product does not have a code");
                    continue;
                }
                $product_code = $prod_conf->code;

                $time_series = $this->extractTimeSeries($csv_files, $product_code);
                if (empty($time_series)) {
                    Log::getInstance()->warning("No time series for code [$product_code]");
                    continue;
                }

                // Generate Records
                [
                    "records" => $records
                ] = $this->generateRecords($product, $time_series, $simulation_epoch);

                // Store
                $n_inserted = $this->storeRecords($product, $records);
                if ($n_inserted === 0) {
                    continue;
                }

                ArgosDB::getInstance()->updateLastSimulation($product->id_product, $simulation_epoch);
                $this->calculateAlerts($product->id_product, $simulation_epoch, AlertPlatform::CITY_ONLY);
            }

            Log::getInstance()->info("Updating data availability...");
            $data_availability_entry = $this->config->monitoring->data_availability_name ?? "";
            DataAvailabilityHelper::updateLastInstant($data_availability_entry, $simulation_epoch);
        } finally {
            FileSystemUtils::removeDirectoryRecursive($output_dir);
        }

        return;
    }

    private function listAvailableFiles(): array
    {
        $aws_credentials = EnvironmentHelper::awsS3FmiCrexdataCredentials();

        // Files are organized based on the date
        $now = new DateTimeImmutable("now", new DateTimeZone("UTC"));

        $client = new FmiCrexdataS3Client($aws_credentials);
        return $client->listAvailableFiles($now);
    }

    private function downloadFiles(array $files, string $output_dir): void
    {
        $aws_credentials = EnvironmentHelper::awsS3FmiCrexdataCredentials();
        $client = new FmiCrexdataS3Client($aws_credentials);

        $client->downloadFiles($files, $output_dir);
    }

    /**
     * Get the files from the most recent simulation
     *
     * @param string[] $files
     * @return array{'simulation': DateTimeImmutable, 'files': string[]}
     */
    public function getMostRecentFiles(array $files): array
    {
        $latest_files = [];

        foreach ($files as $file) {
            // Extract the date from the filename, ex: forecasts/2025-04-22/forecasts_1H_MEPS_Erica_2025-04-22_00.csv
            if (!preg_match_all('/MEPS_(?<Category>.*)_(?<Date>\d{4}-\d{2}-\d{2})_(?<Hour>\d{2})/', $file, $matches, PREG_SET_ORDER)) {
                Log::getInstance()->warning("Could not extract simulation date from file [$file]");
                continue;
            }

            $date_str = $matches[0]["Date"] . " " . $matches[0]["Hour"] . ":00:00Z";
            $date = DateTimeImmutable::createFromFormat("Y-m-d H:i:sZ", $date_str);
            if ($date === false) {
                Log::getInstance()->warning("Could not parse file date string [$date_str]");
                continue;
            }

            $category = $matches[0]["Category"];

            if (!isset($latest_files[$category]) || $date > $latest_files[$category]["date"]) {
                $latest_files[$category] = [
                    "date" => $date,
                    "file" => $file
                ];
            }
        }

        return [
            "simulation" => max(array_column($latest_files, "date")),
            "files" => array_column($latest_files, "file")
        ];
    }

    /**
     * Parse the data from the csv to an array of time series
     *
     * @return array $product_data
     * @throws CsvException
     */
    private function extractTimeSeries(array $csv_files, string $prod_code): array
    {
        $time_series = [];

        foreach ($csv_files as $csv_file) {
            if (!str_contains($csv_file, $prod_code)) {
                continue;
            }

            Log::getInstance()->debug("Parsing file [$csv_file]");
            $data = CsvHelper::parseFile($csv_file, ",", 0);

            foreach ($data->records as $record) {
                $value = $record["Risk Class"];
                if (!is_numeric($value)) {
                    continue;
                }

                $station_code = (string) $record["Municipality"];
                $date_time = new DateTimeImmutable($record["Time"], new DateTimeZone(self::TIME_ZONE));
                if ($date_time === false) {
                    Log::getInstance()->warning("Could not parse date string [" . $record["Time"] . "]");
                    continue;
                }

                $epoch = $date_time->getTimestamp();

                $time_series[$station_code][$epoch] = (float) $value;
            }
        }

        return $time_series;
    }
}
