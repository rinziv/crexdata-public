import threading
import random
import time
import math
import csv
import json
import sys
import pickle
import logging
import os
import requests

import numpy as np
import pandas as pd

from deap import base
from deap import creator
from deap import tools
from deap import algorithms

import ga_utils

# Option 1: Use kafka-python-ng (recommended - install: pip install kafka-python-ng)
from kafka import KafkaConsumer, TopicPartition
from kafka import KafkaProducer

# Option 2: Use confluent-kafka (uncomment below and comment above)
# from confluent_kafka import Consumer, Producer
# Note: confluent-kafka requires different implementation patterns


###
### Initiate local kafka by commencing 'docker run -p 9092:9092 apache/kafka:latest'
###

experiment_folder = os.path.join('./', 'exp_folder')
logging.basicConfig(
    format='%(message)s',
    filename=os.path.join(experiment_folder, "generations.log"),
    level=logging.INFO
)
transformer = None


class Transformer:

    def __init__(self, ga_params, clf=None, scaler=None):
        self.ga_params = ga_params

    def mutate(self, population, indpb):
        """
        Mutates the values in list individual with probability indpb
        """
        for i, param in enumerate(self.ga_params):
            individual = param.mutate(population[i], mu=0, indpb=indpb)
            population[i] = individual

        return population,

    def cxUniform(self, ind1, ind2, indpb):
        for _ in range(100):
            c1, c2 = tools.cxUniform(ind1, ind2, indpb)

        return (c1, c2)

    def random_params(self):
        draws = []
        for p in self.ga_params:
            draws.append(round(p.randomDraw(), 2))

        return draws

    def parse_init_params(self, params_file):
        init_params = []
        with open(params_file) as f_in:
            reader = csv.reader(f_in)
            header = next(reader)
            for row in reader:
                init_params.append(dict(zip(header, row)))
        return init_params


def printf(val):
    print(val)
    sys.stdout.flush()


def obj_func(x):
    """Placeholder objective function"""
    return 0


def num(s):
    """Convert string to int or float"""
    try:
        return int(s)
    except ValueError:
        return float(s)


def create_list_of_json_strings(list_of_lists, super_delim=";"):
    """Create string of ; separated jsonified maps"""
    res = []
    global transformer
    for l in list_of_lists:
        jmap = {}
        for i, p in enumerate(transformer.ga_params):
            jmap[p.name] = l[i]

        jstring = json.dumps(jmap)
        res.append(jstring)

    return (super_delim.join(res))


def queue_map(obj_func, pops):
    """
    Send population to server and receive fitness scores from HTTP response
    """
    global proc_id
    
    if not pops:
        return [(999.0,) for _ in pops]

    url = "https://server.crexdata.eu/webapi/DEFAULT/api/v1/services/barrieroptimization/create-barrier-height-simulations/?example=example"

    payload = {
        "data": [
            {
                "optimizerRequestID": proc_id,
                "parameters": json.dumps(pops)
            }
        ]
    }
    headers = {'Content-type': 'application/json'}

    print(f"Submitting {len(pops)} individuals...")
    
    try:
        r = requests.post(url, json=payload, headers=headers, timeout=900)
        
        print(f"Status Code: {r.status_code}")
        print(f"Response Body: {r.text}")
        
        if r.status_code == 200:
            response_data = r.json()
            
            # Extract from HTTP response
            if 'data' in response_data and len(response_data['data']) > 0:
                data_item = response_data['data'][0]
                
                # Verify optimizer ID
                if data_item.get('optimizerRequestID') == proc_id:
                    result = data_item.get("results")
                    
                    if result:
                        # Parse if string: "[0.214, 0.214, ...]"
                        if isinstance(result, str):
                            result = json.loads(result)
                        
                        print(f"✓ Received {len(result)} fitness scores from HTTP: {result}")
                        
                        # CRITICAL: Convert to list of tuples for DEAP
                        fitness_tuples = [(float(score),) for score in result]
                        
                        print(f"✓ Converted to DEAP format: {fitness_tuples}")
                        return fitness_tuples
                    else:
                        print("⚠ No results field in response")
                else:
                    print(f"⚠ Optimizer ID mismatch: expected {proc_id}, got {data_item.get('optimizerRequestID')}")
            else:
                print("⚠ Invalid response structure")
        else:
            print(f"⚠ HTTP error: {r.status_code}")
        
        # If we get here, something went wrong
        print("✗ Failed to get fitness scores, returning penalty values")
        return [(999.0,) for _ in pops]
        
    except Exception as e:
        print(f"✗ Error: {e}")
        import traceback
        traceback.print_exc()
        return [(999.0,) for _ in pops]


def make_random_parameters():
    """Performs initial random draw on each parameter"""
    return transformer.random_params()


def custom_mutate(individual, indpb):
    """Mutates the values in list individual with probability indpb"""
    return transformer.mutate(individual, indpb)


def cxUniform(ind1, ind2, indpb):
    return transformer.cxUniform(ind1, ind2, indpb)


def timestamp(scores):
    return str(time.time())


def eaSimpleExtended(population, toolbox, cxpb, mutpb, term, ngen, stats=None,
                     halloffame=None, verbose=__debug__, checkpoint=None):
    """Extended evolutionary algorithm with checkpointing"""
    visited_inds = {}
    
    if checkpoint:
        with open(checkpoint, "rb") as cp_file:
            cp = pickle.load(cp_file)
        population = cp["population"]
        halloffame = cp["halloffame"]
        logbook = cp["logbook"]
        random.setstate(cp["rndstate"])
    else:
        logbook = tools.Logbook()
        logbook.header = ['gen', 'nevals'] + (stats.fields if stats else [])

    # Evaluate the individuals with an invalid fitness
    invalid_ind = [ind for ind in population 
                   if (not ind.fitness.valid) and (str(ind) not in visited_inds)]
    fitnesses = toolbox.map(toolbox.evaluate, invalid_ind)
    
    for ind, fit in zip(invalid_ind, fitnesses):
        ind.fitness.values = fit
        visited_inds[str(ind)] = fit

    if halloffame is not None:
        halloffame.update(population)
    
    record = stats.compile(population) if stats else {}
    logbook.record(gen=0, nevals=len(invalid_ind), **record)
    
    if verbose:
        for p in population:
            logging.debug("0, {}, {}, {}".format(0, p, p.fitness))
    
    logging.info("Initial Generation fitness variance = {}".format(
        math.pow(float(logbook.select("std")[-1]), 2)))
    logging.debug("Term crit type: {}".format(type(ngen)))
    
    if term == 'genmax':  # Run for ngens
        logging.debug("Following normal termination criterion process.")
        
        for gen in range(1, ngen + 1):
            offspring = toolbox.select(population, len(population))
            offspring = algorithms.varAnd(offspring, toolbox, cxpb, mutpb)
            
            for ind in offspring:
                if str(ind) in visited_inds:
                    ind.fitness.values = visited_inds[str(ind)]
            
            invalid_ind = [ind for ind in offspring 
                          if (not ind.fitness.valid) and (str(ind) not in visited_inds)]
            fitnesses = toolbox.map(toolbox.evaluate, invalid_ind)
            
            for ind, fit in zip(invalid_ind, fitnesses):
                ind.fitness.values = fit
                visited_inds[str(ind)] = fit

            if halloffame is not None:
                halloffame.update(offspring)

            population[:] = offspring

            record = stats.compile(population) if stats else {}
            logbook.record(gen=gen, nevals=len(invalid_ind), **record)
            
            cp = dict(population=population, generation=gen, halloffame=halloffame,
                     logbook=logbook, rndstate=random.getstate())
            
            with open(checkpoint_file, "wb") as cp_file:
                pickle.dump(cp, cp_file)
            
            logging.info("Generation {} Stored at {}".format(
                gen, time.strftime("%H:%M:%S", time.localtime())))
            
            if verbose:
                printf("Logbookstream: {}\nhalloffame: {}\n".format(
                    logbook.stream, halloffame))
                for p in population:
                    logging.debug("0, {}, {}, {}".format(gen, p, p.fitness))
                for h in halloffame:
                    logging.debug("-1, {}, {}, {}".format(gen, h, h.fitness))
    
    else:  # Run while population fitness variance is less than limit
        counter = 0
        gen = 1
        
        while counter < termination_args:
            logging.debug("Into while, counter = {}".format(counter))
            
            offspring = toolbox.select(population, len(population))
            offspring = algorithms.varAnd(offspring, toolbox, cxpb, mutpb)
            
            for ind in offspring:
                if str(ind) in visited_inds:
                    ind.fitness.values = visited_inds[str(ind)]
            
            invalid_ind = [ind for ind in offspring 
                          if (not ind.fitness.valid) and (str(ind) not in visited_inds)]
            fitnesses = toolbox.map(toolbox.evaluate, invalid_ind)
            
            for ind, fit in zip(invalid_ind, fitnesses):
                ind.fitness.values = fit
                visited_inds[str(ind)] = fit
            
            if halloffame is not None:
                halloffame.update(offspring)

            population[:] = offspring

            record = stats.compile(population) if stats else {}
            logbook.record(gen=gen, nevals=len(invalid_ind), **record)
            
            cp = dict(population=population, generation=gen, halloffame=halloffame,
                     logbook=logbook, rndstate=random.getstate())
            
            with open(checkpoint_file, "wb") as cp_file:
                pickle.dump(cp, cp_file)
            
            logging.info("Generation {} Stored at {}".format(
                gen, time.strftime("%H:%M:%S", time.localtime())))
            
            if verbose:
                printf("Logbookstream: {}\nhalloffame: {}\n".format(
                    logbook.stream, halloffame))
                for p in population:
                    logging.debug("0, {}, {}, {}".format(gen, p, p.fitness))
                for h in halloffame:
                    logging.debug("-1, {}, {}, {}".format(gen, h, h.fitness))
            
            if term == "fitmin":
                if float(logbook.select("min")[-1]) <= ngen:
                    counter += 1
                else:
                    counter = 0
            elif term == "fitvar":
                if math.pow(float(logbook.select("std")[-1]), 2) <= ngen:
                    counter += 1
                else:
                    counter = 0
            elif term == "fitavg":
                if float(logbook.select("avg")[-1]) <= ngen:
                    counter += 1
                else:
                    counter = 0
            else:
                logging.info("Unknown GA configuration value: '{}'... Exiting".format(term))
                counter = termination_args

            logging.debug("Generation fitness variance = {}, counter is now: {}".format(
                math.pow(float(logbook.select("std")[-1]), 2), counter))
            gen += 1

    logging.info("{}\n".format(logbook.stream))
    return population, logbook


def run():
    """Main GA execution function"""
    global ga_config, ga_params, termination_crit, termination_args
    global crossover_prob, mutation_prob, tournament_size, pop_num
    global checkpoint_file, transformer
    
    seed = 1234567

    distance_type_id = ga_config['distance_type']
    logging.info("Crossover probability: {}, Mutation probability: {}, Tournament size: {}".format(
        crossover_prob, mutation_prob, tournament_size))
    logging.info("No. of population: {}, Random seed: {}, GA parameters: {}".format(
        pop_num, seed, ga_params))
    logging.info("Distance type - [{}]\t Termination criterion - [{}] - args [{}]\n".format(
        distance_type_id, termination_crit, termination_args))
    logging.info("Begin at: {}".format(time.strftime("%H:%M:%S", time.localtime())))
    
    random.seed(seed)
    ga_parameters = ga_utils.create_parameters(ga_params)
    transformer = Transformer(ga_parameters)

    # DEAP class creators
    creator.create("FitnessMin", base.Fitness, weights=(-1.0,))
    creator.create("Individual", list, fitness=creator.FitnessMin)

    # DEAP method definitions
    toolbox = base.Toolbox()
    toolbox.register("individual", tools.initIterate, creator.Individual,
                     make_random_parameters)
    toolbox.register("population", tools.initRepeat, list, toolbox.individual)
    toolbox.register("evaluate", obj_func)
    toolbox.register("mate", cxUniform, indpb=crossover_prob)
    toolbox.register("mutate", custom_mutate, indpb=mutation_prob)
    toolbox.register("select", tools.selTournament, tournsize=tournament_size)
    toolbox.register("map", queue_map)

    pop = toolbox.population(n=pop_num)

    print("\n\n\n {} \n\n\n\n".format(pop))

    hof = tools.HallOfFame(pop_num)
    stats = tools.Statistics(lambda ind: ind.fitness.values)
    stats.register("avg", np.mean)
    stats.register("std", np.std)
    stats.register("min", np.min)
    stats.register("max", np.max)
    stats.register("ts", timestamp)

    start_time = time.time()
    pop, log = eaSimpleExtended(pop, toolbox, cxpb=crossover_prob, mutpb=mutation_prob,
                               term=termination_crit, ngen=num(termination_args),
                               stats=stats, halloffame=hof, verbose=True, checkpoint=None)
    end_time = time.time()

    fitnesses = [str(p.fitness.values[0]) for p in pop]
    logging.info("Logbook: \n{}".format(log))
    logging.info("\n Hall of Fame: \n")
    logging.info("End at: {}".format(time.strftime("%H:%M:%S", time.localtime())))
    
    for h in hof:
        logging.debug("-1, {}, {}, {}".format(-1, h, h.fitness))
    
    producer = KafkaProducer(
        bootstrap_servers=broker_ip,
        value_serializer=lambda v: json.dumps(v).encode('utf-8')
    )

    producer.send(output_topic, "proc_ID: {}, barrier_heights: {}, fitness_score: {}".format(proc_id, hof[0], hof[0].fitness.values[0]))
    # for h in hof:
    #     producer.send(output_topic, "{},{},{}".format(proc_id, h, h.fitness))


def rm_main(data):
    """Main entry point for the GA optimizer"""
    global ga_config, ga_params, broker_ip, config_topic, input_topic, output_topic
    global termination_crit, termination_args, crossover_prob, mutation_prob
    global tournament_size, pop_num, checkpoint_file, consumer, proc_id

    broker_ip = 'kafka-no-auth:9094'
    config_topic = 'ga_config'
    input_topic = 'ga_in'
    output_topic = 'ga_out'

    print("Broker IP: {}, Config Topic: {}, Input Topic: {}, Output Topic: {}".format(
        broker_ip, config_topic, input_topic, output_topic))

    configurator = KafkaConsumer(
        config_topic,
        bootstrap_servers=broker_ip,
        value_deserializer=lambda m: json.loads(m.decode('utf-8')),
        auto_offset_reset='latest',
        enable_auto_commit=True
    )

    consumer = KafkaConsumer(
        input_topic,
        bootstrap_servers=broker_ip,
        value_deserializer=lambda m: json.loads(m.decode('utf-8')),
        auto_offset_reset='latest',
        enable_auto_commit=True
    )

    partition = TopicPartition(config_topic,0)
    end_offset = configurator.end_offsets([partition])
    configurator.seek(partition,list(end_offset.values())[0]-1)
    
    while True:
        for message1 in configurator:
            try:
                print("Initiating simulations with configurations: {}".format(message1.value))
                
                # The config is inside 'config_topic' key and is a JSON string
                config_string = message1.value['config_topic']
                received = json.loads(config_string)  # Parse the JSON string
                
                proc_id = received['process_id']
                ga_config = received['ga_config.json']
                print("Loaded configuration: {}".format(ga_config))
                
                termination_crit = ga_config['termination_crit']
                termination_args = ga_config['termination_args']
                crossover_prob = float(ga_config['crossover_prob'])
                mutation_prob = float(ga_config['mutation_prob'])
                tournament_size = int(ga_config['tournament_size'])
                pop_num = int(ga_config['pop_num'])
                checkpoint_file = os.path.join(experiment_folder, "ga_checkpoint.pkl")
                
                # The chromosome def is inside ga_config.json, not at root level
                ga_params = ga_config['ga_chromosome_def']
                print("Loaded chromosome definitions: {}".format(ga_params))
                
                run()
                print("\nOptimization Completed!\n")
                return pd.DataFrame({'values': [3, 5, 77, 8]})

            except KeyError as e:
                print(f"KeyError: {e}")
                print("Malformed message! Ignoring...")
            except Exception as e:
                print(f"Unexpected error: {e}")
