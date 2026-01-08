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

from kafka import KafkaConsumer
from kafka import KafkaProducer



###
### Initiate local kafka by commencing 'docker run -p 9092:9092 apache/kafka:latest'
###


experiment_folder = os.path.join('./','exp_folder')
logging.basicConfig(format='%(message)s',filename=os.path.join(experiment_folder,"generations.log"),level=logging.INFO)
transformer = None

class Transformer:

    def __init__(self, ga_params, clf = None, scaler = None):
        self.ga_params = ga_params

    def mutate(self, population, indpb):
        """
        Mutates the values in list individual with probability indpb
        """
        # Note, if we had some aggregate constraint on the individual
        # (e.g. individual[1] * individual[2] < 10), we could copy
        # individual into a temporary list and mutate though until the
        # constraint was satisfied
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
            #draws.append(round(p.randomDraw(),2)) # if we wish to round, e.g. to 2 decimal digits
            draws.append(round(p.randomDraw(),2))

        return draws

    def parse_init_params(self, params_file):
        init_params = []
        with open(params_file) as f_in:
            reader = csv.reader(f_in)
            header = next(reader)
            for row in reader:
                init_params.append(dict(zip(header,row)))
        return init_params
        
def printf(val):
    print(val)
    sys.stdout.flush()

# Not used
def obj_func(x):
    return 0

def num(s):
    try:
        return int(s)
    except ValueError:
        return float(s)


def create_list_of_json_strings(list_of_lists, super_delim=";"):
    # create string of ; separated jsonified maps
    res = []
    global transformer
    for l in list_of_lists:
        jmap = {}
        for i,p in enumerate(transformer.ga_params):
            jmap[p.name] = l[i]

        jstring = json.dumps(jmap)
        res.append(jstring)

    return (super_delim.join(res))


#
#   Here, fitnesses must be received
#
def queue_map(obj_func, pops):
    global proc_id
    # Note that the obj_func is not used
    # sending data that looks like:
    # [[a,b,c,d],[e,f,g,h],...]
    if not pops:
        return []


    url = "https://server.crexdata.eu/webapi/DEFAULT/api/v1/services/start-simulations/start-simulations"
    
    payload = {"data": [{"optimizerRequestID":"\""proc_id"\""},{"parameters":"\""pops"\""}]}
    headers = {'Content-type': 'application/json'}
    r = requests.post(url, data=payload, headers=headers)
    print("Status Code:", r.status_code)
    print("Response Body:", r.text)

    

    # Receive population fitness scores
    while True:
        for message1 in consumer:
            try:
                check = json.loads(message1.value["optimizerRequestID"])
                if check == proc_id:
                    result = json.loads(message1.value["results"])
                    print("Received fitness scores: {}".format(result))
                    return result
                    # producer.send(output_topic, json.dumps(
                                    # {"fitness_scores": "a bunch of fitnesses"}))
            except KeyError as e:
                print(e)
                print("Malformed message! Ignoring...")
                return 'N/A'



def make_random_parameters():
    """
    Performs initial random draw on each parameter
    """
    return transformer.random_params()

# Returns a tuple of one individual
def custom_mutate(individual, indpb):
    """
    Mutates the values in list individual with probability indpb
    """
    return transformer.mutate(individual, indpb)


def cxUniform(ind1, ind2, indpb):
    return transformer.cxUniform(ind1, ind2, indpb)

def timestamp(scores):
    return str(time.time())

def eaSimpleExtended(population, toolbox, cxpb, mutpb, term, ngen, stats=None,
             halloffame=None, verbose=__debug__, checkpoint=None):
    visited_inds = {}
    # If previous sim has failed, or we want to continue from previous generation
    if checkpoint:
        # A file name has been given, then load the data from the file
        with open(checkpoint, "rb") as cp_file:
            cp = pickle.load(cp_file)
        population = cp["population"]
        #start_gen = cp["generation"]
        halloffame = cp["halloffame"]
        logbook = cp["logbook"]
        random.setstate(cp["rndstate"])
    else:
        logbook = tools.Logbook()
        logbook.header = ['gen', 'nevals'] + (stats.fields if stats else [])

    # Evaluate the individuals with an invalid fitness
    # invalid_ind = [ind for ind in population if not ind.fitness.valid]
    invalid_ind = [ind for ind in population if (not ind.fitness.valid) and (not str(ind) in visited_inds)]
    fitnesses = toolbox.map(toolbox.evaluate, invalid_ind)
    for ind, fit in zip(invalid_ind, fitnesses):
        ind.fitness.values = fit
        visited_inds[str(ind)] = fit

    if halloffame is not None:
        halloffame.update(population)
    # gen_variance = []
    # variance_log = []
    record = stats.compile(population) if stats else {}
    logbook.record(gen=0, nevals=len(invalid_ind), **record)
    if verbose:
        # printf("Logbookstream: {}\nhalloffame: {}\n".format(logbook.stream, halloffame))
        for p in population:
            logging.debug("0, {}, {}, {}".format(0, p, p.fitness))
    # for p in population:
    #     gen_variance.append(p.fitness.values)
    # variance_log.append(np.var(gen_variance))
    logging.info("Initial Generation fitness variance = {}".format(math.pow(float(logbook.select("std")[-1]),2)))
    # logging.debug("Stats: {}".format(stats))
    # logging.debug("Record: {}, length: {}".format(record, len(record)))
    logging.debug("Term crit type: {}".format(type(ngen)))
    if term=='genmax': # Run for ngens
        logging.debug("Following normal termination criterion process.")
        # Begin the generational process
        for gen in range(1, ngen + 1):
            # Select the next generation individuals
            offspring = toolbox.select(population, len(population))

            # Vary the pool of individuals
            offspring = algorithms.varAnd(offspring, toolbox, cxpb, mutpb)
            for ind in offspring:
                try:
                    ind.fitness.values = visited_inds[str(ind)]
                except KeyError:
                    pass
            # Evaluate the individuals with an invalid fitness
            # invalid_ind = [ind for ind in offspring if not ind.fitness.valid]
            invalid_ind = [ind for ind in offspring if (not ind.fitness.valid) and (not str(ind) in visited_inds)]
            fitnesses = toolbox.map(toolbox.evaluate, invalid_ind)
            for ind, fit in zip(invalid_ind, fitnesses):
                ind.fitness.values = fit
                visited_inds[str(ind)] = fit

            # Update the hall of fame with the generated individuals
            if halloffame is not None:
                halloffame.update(offspring)

            # Replace the current population by the offspring
            population[:] = offspring

            # Append the current generation statistics to the logbook
            record = stats.compile(population) if stats else {}
            logbook.record(gen=gen, nevals=len(invalid_ind), **record)
            # Fill the dictionary using the dict(key=value[, ...]) constructor
            cp = dict(population=population, generation=gen, halloffame=halloffame,
                       logbook=logbook, rndstate=random.getstate())
            with open(checkpoint_file, "wb") as cp_file:
                pickle.dump(cp, cp_file)
            logging.info("Generation {} Stored at {}".format(gen, time.strftime("%H:%M:%S", time.localtime())))
            if verbose:
                printf("Logbookstream: {}\nhalloffame: {}\n".format(logbook.stream, halloffame))
                for p in population:
                    logging.debug("0, {}, {}, {}".format(gen, p, p.fitness))
                for h in halloffame:
                    logging.debug("-1, {}, {}, {}".format(gen, h, h.fitness))
    else: # Run while population fitness variance is less than limit for "termination_args" consecutive generations
        # Begin the generational process
        counter = 0
        gen = 1
        while counter<termination_args:
            logging.debug("Into while, counter = {}".format(counter))
            # gen_variance = []
            # Select the next generation individuals
            offspring = toolbox.select(population, len(population))

            # Vary the pool of individuals
            offspring = algorithms.varAnd(offspring, toolbox, cxpb, mutpb)
            for ind in offspring:
                try:
                    ind.fitness.values = visited_inds[str(ind)]
                except KeyError:
                    pass
            # Evaluate the individuals with an invalid fitness
            # invalid_ind = [ind for ind in offspring if not ind.fitness.valid]
            invalid_ind = [ind for ind in offspring if (not ind.fitness.valid) and (not str(ind) in visited_inds)]
            fitnesses = toolbox.map(toolbox.evaluate, invalid_ind)
            for ind, fit in zip(invalid_ind, fitnesses):
                ind.fitness.values = fit
                visited_inds[str(ind)] = fit
            
            # Update the hall of fame with the generated individuals
            if halloffame is not None:
                halloffame.update(offspring)

            # Replace the current population by the offspring
            population[:] = offspring

            # Append the current generation statistics to the logbook
            record = stats.compile(population) if stats else {}
            logbook.record(gen=gen, nevals=len(invalid_ind), **record)
            # Fill the dictionary using the dict(key=value[, ...]) constructor
            cp = dict(population=population, generation=gen, halloffame=halloffame,
                       logbook=logbook, rndstate=random.getstate())
            with open(checkpoint_file, "wb") as cp_file:
                pickle.dump(cp, cp_file)
            logging.info("Generation {} Stored at {}".format(gen, time.strftime("%H:%M:%S", time.localtime())))
            if verbose:
                printf("Logbookstream: {}\nhalloffame: {}\n".format(logbook.stream, halloffame))
                for p in population:
                    logging.debug("0, {}, {}, {}".format(gen, p, p.fitness))
                for h in halloffame:
                    logging.debug("-1, {}, {}, {}".format(gen, h, h.fitness))
            # for p in population:
            #     gen_variance.append(p.fitness.values)
            # variance_log.append(np.var(gen_variance))
            # if abs(variance_log[-1]-variance_log[-2]) <= ngens:
            if term=="fitmin":
                if float(logbook.select("min")[-1]) <= ngen:
                    counter = counter + 1
                else:
                    counter = 0
            elif term=="fitvar":
                if math.pow(float(logbook.select("std")[-1]),2) <= ngen:
                    counter = counter + 1
                else:
                    counter = 0
            elif term=="fitavg":
                if float(logbook.select("avg")[-1]) <= ngen:
                    counter = counter + 1
                else:
                    counter = 0
            else:
                logging.info("Unknown GA configuration value: '{}'... Exiting".format(term))
                counter = termination_args

            logging.debug("Generation fitness variance = {}, counter is now: {}".format(math.pow(float(logbook.select("std")[-1]),2), counter))
            gen = gen + 1

    logging.info("{}\n".format(logbook.stream))
    return population, logbook

def run():
    """
    :param num_iterations: number of generations
    :param seed: random seed
    :param ga parameters file name: ga parameters file name (e.g., "ga_params.json")
    :param num_population population of ga algorithm
    """

    global ga_config
    global ga_params
    global broker_ip
    global config_topic
    global input_topic
    global output_topic
    global termination_crit
    global termination_args
    global crossover_prob
    global mutation_prob
    global tournament_size
    global pop_num
    global checkpoint_file
    global consumer
    global proc_id
    seed = 1234567 


    distance_type_id = ga_config['distance_type']
    logging.info("Crossover probability: {}, Mutation probability: {}, Tournament size: {}".format(crossover_prob,mutation_prob,tournament_size))
    logging.info("No. of population: {}, Random seed: {}, GA parameters: {}".format(pop_num, seed, ga_params))
    logging.info("Distance type - [{}]\t Termination criterion - [{}] - args [{}]\n".format(distance_type_id,termination_crit,termination_args))
    logging.info("Begin at: {}".format(time.strftime("%H:%M:%S", time.localtime())))
    # num_iterations not used
    random.seed(seed)
    ga_parameters = ga_utils.create_parameters(ga_params)
    global transformer
    transformer = Transformer(ga_parameters)

    # deap class creators
    creator.create("FitnessMin", base.Fitness, weights=(-1.0,))
    creator.create("Individual", list, fitness=creator.FitnessMin)

    # deap method definitions
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
    pop, log = eaSimpleExtended(pop, toolbox, cxpb=crossover_prob, mutpb=mutation_prob, term=termination_crit, ngen=num(termination_args), stats=stats, halloffame=hof, verbose=True, checkpoint=None)

    end_time = time.time()

    fitnesses = [str(p.fitness.values[0]) for p in pop]
    logging.info("Logbook: \n{}".format(log))
    logging.info("\n Hall of Fame: \n")
    logging.info("End at: {}".format(time.strftime("%H:%M:%S", time.localtime())))
    for h in hof:
        logging.debug("-1, {}, {}, {}".format(-1, h, h.fitness))
    producer = KafkaProducer(bootstrap_servers=broker_ip,
                             value_serializer=lambda v: json.dumps(v).encode('utf-8'))
                             # security_protocol= "SASL_SSL",
                             # sasl_mechanism = "PLAIN",
                             # sasl_plain_username = "user",
                             # sasl_plain_password = "password",
                             # ssl_certfile = "./ssl/client_cert.pem",
                             # ssl_keyfile = "./ssl/client_key.pem",
                             # ssl_cafile = "./ssl/ca.pem",
                             # ssl_password = "crexdata")
    producer.send(output_topic, json.dumps("{},{},{}".format(proc_id,h, h.fitness)))
    



def rm_main(data):

    global ga_config
    global ga_params
    global broker_ip
    global config_topic
    global input_topic
    global output_topic
    global termination_crit
    global termination_args
    global crossover_prob
    global mutation_prob
    global tournament_size
    global pop_num
    global checkpoint_file
    global consumer
    global proc_id

    broker_ip = 'localhost:9092'
    config_topic = 'ga_config'
    input_topic = 'ga_in'
    output_topic = 'ga_out'


    # broker_ip = 'server.crexdata.eu:9092'
    # config_topic = 'ncsr_optimizer_input'
    # input_topic = 'ga_in'
    # output_topic = 'ga_out'


    print( "Broker IP: {}, Config Topic: {}, Input Topic: {}, Output Topic: {}, ".format(
                broker_ip,
                config_topic,
                input_topic,
                output_topic,
                ))

    configurator = KafkaConsumer(config_topic, bootstrap_servers=broker_ip,
                                 value_deserializer=lambda m: json.loads(m.decode('utf-8')),
                                 auto_offset_reset='latest',
                                 enable_auto_commit=True)
                                 # security_protocol= "SASL_SSL",
                                 # sasl_mechanism = "PLAIN",
                                 # sasl_plain_username = "user",
                                 # sasl_plain_password = "password",
                                 # ssl_certfile = "./ssl/client_cert.pem",
                                 # ssl_keyfile = "./ssl/client_key.pem",
                                 # ssl_cafile = "./ssl/ca.pem",
                                 # ssl_password = "crexdata")

    consumer = KafkaConsumer(input_topic, bootstrap_servers=broker_ip,
                                 value_deserializer=lambda m: json.loads(m.decode('utf-8')),
                                 auto_offset_reset='latest',
                                 enable_auto_commit=True)
                                 # security_protocol= "SASL_SSL",
                                 # sasl_mechanism = "PLAIN",
                                 # sasl_plain_username = "user",
                                 # sasl_plain_password = "password",
                                 # ssl_certfile = "./ssl/client_cert.pem",
                                 # ssl_keyfile = "./ssl/client_key.pem",
                                 # ssl_cafile = "./ssl/ca.pem",
                                 # ssl_password = "crexdata")

    while True:
        for message1 in configurator:
            try:
                print("Initiating simulations with configurations: {}".format(message1.value))
                received = json.loads(message1.value)
                proc_id = json.received['process_id']
                ga_config = received['ga_config.json']
                print("Loaded configuration: {}".format(ga_config))
                termination_crit = ga_config['termination_crit']
                termination_args = ga_config['termination_args']
                crossover_prob = float(ga_config['crossover_prob'])
                mutation_prob = float(ga_config['mutation_prob'])
                tournament_size = int(ga_config['tournament_size'])
                pop_num = int(ga_config['pop_num'])
                checkpoint_file = os.path.join(experiment_folder,"ga_checkpoint.pkl")
                ga_params = received['ga_chromosome_def']
                print("Loaded chromosome definitions: {}".format(ga_params))
                run()
                print("\nOptimization Completed!\n")
                return pd.DataFrame({'values': [3, 5, 77, 8]})

            except KeyError as e:
                print(e)
                print("Malformed message! Ignoring...")




